from GcodeManager import GcodeManager
import moderngl
from PIL import Image
import numpy as np
from multiprocessing import Pool, cpu_count

class Renderer:
    def __init__(self, file_path, printer_info):
        self.gcode_manager = GcodeManager(file_path)
        self.printer_info = printer_info
        self._vertex_data = None
        self._grid_vertex_list = None
        self._bounds = None
        self._prepare_geometry()

    VERTEX_SHADER = """
    #version 330

    in vec3 in_position;
    in vec3 in_normal;

    uniform mat4 mvp_matrix;
    uniform mat4 model_matrix;
    uniform mat3 normal_matrix;

    out vec3 frag_position;
    out vec3 frag_normal;

    void main() {
        gl_Position = mvp_matrix * vec4(in_position, 1.0);
        frag_position = (model_matrix * vec4(in_position, 1.0)).xyz;
        frag_normal = normalize(normal_matrix * in_normal);
    }
    """

    FRAGMENT_SHADER = """
    #version 330

    in vec3 frag_position;
    in vec3 frag_normal;

    uniform vec3 line_color;
    uniform vec3 light_position;
    uniform vec3 view_position;
    uniform vec3 light_color;
    uniform vec3 ambient_color;
    uniform vec3 diffuse_color;
    uniform vec3 specular_color;
    uniform float shininess;

    out vec4 fragColor;

    void main() {
        vec3 normal = normalize(frag_normal);
        
        vec3 ambient = ambient_color;
        
        // Diffuse component (Lambertian reflection)
        vec3 light_dir = normalize(light_position - frag_position);
        float diff = max(dot(normal, light_dir), 0.0);
        vec3 diffuse = diff * diffuse_color;
        
        // Specular component (Blinn-Phong)
        vec3 view_dir = normalize(view_position - frag_position);
        vec3 half_dir = normalize(light_dir + view_dir);
        float spec = pow(max(dot(normal, half_dir), 0.0), shininess);
        vec3 specular = spec * specular_color;
        
        // Combine all components and apply light color
        vec3 result = (ambient + diffuse + specular) * light_color;
        fragColor = vec4(result, 1.0);
    }
    """

    @staticmethod
    def look_at(camera_pos, target_pos, up_input):
        camera_pos = np.array(camera_pos, dtype=np.float32)
        target_pos = np.array(target_pos, dtype=np.float32)
        up_input = np.array(up_input, dtype=np.float32)

        forward = target_pos - camera_pos
        forward = forward / np.linalg.norm(forward)

        right = np.cross(forward, up_input)
        right = right / np.linalg.norm(right)

        up = np.cross(right, forward)

        rotate = np.eye(4, dtype=np.float32)
        rotate[0, :3] = right
        rotate[1, :3] = up
        rotate[2, :3] = -forward

        translate = np.eye(4, dtype=np.float32)
        translate[:3, 3] = -camera_pos

        return rotate @ translate

    @staticmethod
    def perspective(fov, aspect, near, far):
        f = 1.0 / np.tan(np.radians(fov) / 2.0)

        matrix = np.zeros((4, 4), dtype=np.float32)
        matrix[0, 0] = f / aspect
        matrix[1, 1] = f
        matrix[2, 2] = (far + near) / (near - far)
        matrix[2, 3] = (2 * far * near) / (near - far)
        matrix[3, 2] = -1.0

        return matrix

    @staticmethod
    def create_mvp_matrix(bounds, camera_pos, width, height):
        # Calculate center
        center_x = (bounds['min_x'] + bounds['max_x']) / 2.0
        center_y = (bounds['min_y'] + bounds['max_y']) / 2.0
        center_z = (bounds['min_z'] + bounds['max_z']) / 2.0

        view_m = Renderer.look_at(camera_pos, [center_x, center_y, center_z], [0, 0, 1])

        longest_axis = np.array([bounds['max_x'], bounds['max_y'], bounds['max_z']])

        proj_m = Renderer.perspective(
            fov=60,
            aspect=width / height,
            near=0.1,
            far=np.sqrt(np.dot(longest_axis, longest_axis)) * 2,
        )

        return proj_m @ view_m

    def get_camera_position(self, desired_view):
        # Calculate model center from bounds
        center_x = (self._bounds['min_x'] + self._bounds['max_x']) / 2.0
        center_y = (self._bounds['min_y'] + self._bounds['max_y']) / 2.0
        center_z = (self._bounds['min_z'] + self._bounds['max_z']) / 2.0
        
        # Calculate model dimensions
        model_width = self._bounds['max_x'] - self._bounds['min_x']
        model_height = self._bounds['max_y'] - self._bounds['min_y']
        model_depth = self._bounds['max_z'] - self._bounds['min_z']
        
        # Calculate distance based on the diagonal of the model to ensure full visibility
        # Add some extra distance for better framing
        model_diagonal = np.sqrt(model_width**2 + model_height**2 + model_depth**2)
        camera_distance = model_diagonal * 1.5
        
        # Camera height should always be above the top of the object
        # Use max_z (top of object) plus a minimum offset to ensure visibility
        # For very short objects, use a minimum height based on the model's horizontal size
        # Err on the side of being higher for better visibility
        min_height_offset = max(model_diagonal * 0.5, model_depth * 1.0, 2.0)  # At least 2 units above
        camera_height = self._bounds['max_z'] + min_height_offset
        
        # Calculate horizontal distance (projection of camera_distance onto XY plane)
        # This ensures cameras are at the same height and equidistant from center
        horizontal_distance = np.sqrt(camera_distance**2 - (camera_height - center_z)**2)
        if horizontal_distance <= 0:
            horizontal_distance = camera_distance
        
        # Normalize direction vectors for cardinal and diagonal directions
        sqrt2_inv = 1.0 / np.sqrt(2.0)
        
        match desired_view:
            case "NORTH":
                # Looking south (positive Y direction)
                direction = np.array([0.0, 1.0, 0.0], dtype=np.float32)
            case "EAST":
                # Looking west (negative X direction)
                direction = np.array([-1.0, 0.0, 0.0], dtype=np.float32)
            case "SOUTH":
                # Looking north (negative Y direction)
                direction = np.array([0.0, -1.0, 0.0], dtype=np.float32)
            case "WEST":
                # Looking east (positive X direction)
                direction = np.array([1.0, 0.0, 0.0], dtype=np.float32)
            case "NORTH_WEST":
                # Looking southeast (positive X, positive Y)
                direction = np.array([sqrt2_inv, sqrt2_inv, 0.0], dtype=np.float32)
            case "NORTH_EAST":
                # Looking southwest (negative X, positive Y)
                direction = np.array([-sqrt2_inv, sqrt2_inv, 0.0], dtype=np.float32)
            case "SOUTH_EAST":
                # Looking northwest (negative X, negative Y)
                direction = np.array([-sqrt2_inv, -sqrt2_inv, 0.0], dtype=np.float32)
            case "SOUTH_WEST":
                # Looking northeast (positive X, negative Y)
                direction = np.array([sqrt2_inv, -sqrt2_inv, 0.0], dtype=np.float32)
        
        # Position camera at equidistant distance from center
        camera_pos = np.array([
            center_x + direction[0] * horizontal_distance,
            center_y + direction[1] * horizontal_distance,
            camera_height
        ], dtype=np.float32)
        
        return camera_pos

    def get_grid_vertex_list(self, spacing):
        x_min = self.printer_info['bed_min_x']
        y_min = self.printer_info['bed_min_y']
        x_max = self.printer_info['bed_max_x']
        y_max = self.printer_info['bed_max_y']
        grid_vertex_list = []
        for x in range(x_min, x_max+1, spacing):
            grid_vertex_list.append(x)
            grid_vertex_list.append(y_min)
            grid_vertex_list.append(0)
            grid_vertex_list.append(x)
            grid_vertex_list.append(y_max)
            grid_vertex_list.append(0)
        for y in range(y_min, y_max+1, spacing):
            grid_vertex_list.append(x_min)
            grid_vertex_list.append(y)
            grid_vertex_list.append(0)
            grid_vertex_list.append(x_max)
            grid_vertex_list.append(y)
            grid_vertex_list.append(0)

        # Put an "X" in the origin square for reference
        grid_vertex_list.append(0)
        grid_vertex_list.append(0)
        grid_vertex_list.append(0)

        grid_vertex_list.append(spacing)
        grid_vertex_list.append(spacing)
        grid_vertex_list.append(0)

        grid_vertex_list.append(0)
        grid_vertex_list.append(spacing)
        grid_vertex_list.append(0)

        grid_vertex_list.append(spacing)
        grid_vertex_list.append(0)
        grid_vertex_list.append(0)

        return grid_vertex_list

    def _prepare_geometry(self):
        """Precompute and cache vertex data that doesn't change between views"""
        if self._vertex_data is not None:
            return  # Already prepared
        
        # Calculate bounds once
        self._bounds = self.gcode_manager.get_bounds()
        
        # Prepare grid vertex list
        self._grid_vertex_list = self.get_grid_vertex_list(10)
        
        # Prepare segment vertex data
        num_segments = len(self.gcode_manager.segments)
        # Each segment generates 36 vertices (12 triangles × 3 vertices)
        vertices_per_segment = 36
        total_vertices = num_segments * vertices_per_segment
        
        # Pre-allocate numpy arrays for better performance
        vertex_array = np.empty((total_vertices, 3), dtype=np.float32)
        normal_array = np.empty((total_vertices, 3), dtype=np.float32)
        
        # Process segments in parallel using multiprocessing
        num_workers = max(1, cpu_count() - 1)  # Leave one core free
        
        # Create worker arguments: (segment_index, segment, width, height)
        worker_args = [(i, segment, 0.3, 0.3) for i, segment in enumerate(self.gcode_manager.segments)]
        
        # Process segments in parallel
        with Pool(processes=num_workers) as pool:
            results = pool.starmap(_process_segment_worker, worker_args)
        
        # Assign results to pre-allocated arrays (results are in order due to starmap)
        for i, (segment_index, vertices, normals) in enumerate(results):
            start_idx = segment_index * vertices_per_segment
            end_idx = start_idx + vertices_per_segment
            vertex_array[start_idx:end_idx] = vertices
            normal_array[start_idx:end_idx] = normals

        # Interleave vertices and normals using numpy for efficiency
        # Interleave using numpy: [v0, n0, v1, n1, ...]
        # Stack: vertex_array is (N, 3), normal_array is (N, 3)
        # We want (N, 6) where each row is [vx, vy, vz, nx, ny, nz]
        interleaved = np.hstack([vertex_array, normal_array])
        # Flatten to 1D: [vx, vy, vz, nx, ny, nz, vx, vy, vz, nx, ny, nz, ...]
        self._vertex_data = interleaved.flatten()

    @staticmethod
    def get_segment_vertices(segment, width, height):
        """Generate vertices and normals for a segment. Returns numpy arrays."""
        start = np.array(segment.start, dtype=np.float32)
        end = np.array(segment.end, dtype=np.float32)
        
        # Calculate direction vector
        direction = end - start
        direction_norm = np.linalg.norm(direction)
        if direction_norm > 0:
            direction = direction / direction_norm
        else:
            direction = np.array([1.0, 0.0, 0.0], dtype=np.float32)

        up = np.array([0, 0, 1], dtype=np.float32)
        # Calculate right vector (perpendicular to direction)
        right = np.cross(direction, up)
        right_norm = np.linalg.norm(right)
        if right_norm > 0:
            right = right / right_norm
        else:
            # Fallback if direction is parallel to up
            right = np.array([1.0, 0.0, 0.0], dtype=np.float32)
        
        half_width = width / 2.0
        half_height = height / 2.0

        # Pre-allocate arrays (36 vertices = 12 triangles × 3)
        segment_vertices = np.empty((36, 3), dtype=np.float32)
        segment_normals = np.empty((36, 3), dtype=np.float32)

        # Calculate corner vertices
        start_v = np.array([
            start + (half_height * up),
            start + (half_width * right),
            start + (half_height * -up),
            start + (half_width * -right)
        ], dtype=np.float32)

        end_v = np.array([
            end + (half_height * up),
            end + (half_width * right),
            end + (half_height * -up),
            end + (half_width * -right)
        ], dtype=np.float32)

        def calculate_normal(v0, v1, v2):
            """Calculate face normal for a triangle"""
            edge1 = v1 - v0
            edge2 = v2 - v0
            normal = np.cross(edge1, edge2)
            norm = np.linalg.norm(normal)
            if norm > 0:
                return normal / norm
            return np.array([0.0, 0.0, 1.0], dtype=np.float32)

        def add_triangle(idx, v0, v1, v2):
            """Add triangle vertices and calculate normal at index"""
            segment_vertices[idx] = v0
            segment_vertices[idx + 1] = v1
            segment_vertices[idx + 2] = v2
            normal = calculate_normal(v0, v1, v2)
            segment_normals[idx] = normal
            segment_normals[idx + 1] = normal
            segment_normals[idx + 2] = normal

        TOP = 0
        RIGHT = 1
        BOTTOM = 2
        LEFT = 3

        idx = 0
        #start cap
        add_triangle(idx, start_v[BOTTOM], start_v[RIGHT], start_v[TOP]); idx += 3
        add_triangle(idx, start_v[LEFT], start_v[BOTTOM], start_v[TOP]); idx += 3

        #end cap
        add_triangle(idx, end_v[TOP], end_v[RIGHT], end_v[BOTTOM]); idx += 3
        add_triangle(idx, end_v[TOP], end_v[BOTTOM], end_v[LEFT]); idx += 3

        #top right
        add_triangle(idx, start_v[RIGHT], end_v[TOP], start_v[TOP]); idx += 3
        add_triangle(idx, end_v[RIGHT], end_v[TOP], start_v[RIGHT]); idx += 3
        
        #top left
        add_triangle(idx, start_v[LEFT], end_v[TOP], start_v[TOP]); idx += 3
        add_triangle(idx, end_v[LEFT], end_v[TOP], start_v[LEFT]); idx += 3

        #bottom right
        add_triangle(idx, start_v[RIGHT], end_v[RIGHT], start_v[BOTTOM]); idx += 3
        add_triangle(idx, end_v[BOTTOM], start_v[BOTTOM], end_v[RIGHT]); idx += 3

        #bottom left
        add_triangle(idx, start_v[LEFT], end_v[BOTTOM], start_v[BOTTOM]); idx += 3
        add_triangle(idx, end_v[LEFT], end_v[BOTTOM], start_v[LEFT]); idx += 3

        return segment_vertices, segment_normals

    def render_view(self, desired_view, width, height):
        # Ensure geometry is prepared
        self._prepare_geometry()
        
        # Use EGL backend for headless rendering (works in Docker without X11)
        ctx = None
        try:
            ctx = moderngl.create_standalone_context(backend='egl', require=330)

            # Create depth buffer for proper z-ordering
            # Enable 4x MSAA for smoother, less grainy output
            samples = 4
            depth_buffer = ctx.depth_renderbuffer((width, height), samples=samples)
            frame_buffer = ctx.framebuffer([ctx.renderbuffer((width, height), components=4, samples=samples)], depth_buffer)
            output = ctx.framebuffer([ctx.renderbuffer((width, height), components=4)])
            
            # Enable depth testing for proper layer ordering
            ctx.enable(moderngl.DEPTH_TEST)

            prog = ctx.program(
                vertex_shader=Renderer.VERTEX_SHADER,
                fragment_shader=Renderer.FRAGMENT_SHADER
            )

            # Create buffers from cached data
            grid_vertex_buffer = ctx.buffer(np.array(self._grid_vertex_list).astype('f4').tobytes())
            grid_vertex_array = ctx.vertex_array(prog, [(grid_vertex_buffer, '3f', 'in_position')])

            vertex_buffer = ctx.buffer(self._vertex_data.tobytes())
            vertex_array = ctx.vertex_array(prog, [
                (vertex_buffer, '3f 3f', 'in_position', 'in_normal')
            ])
            
            # Get camera position for view position
            camera_pos = self.get_camera_position(desired_view)
            mvp_matrix = self.create_mvp_matrix(self._bounds, camera_pos, width, height)
            
            # Create model matrix (identity for now, can be used for transformations)
            model_matrix = np.eye(4, dtype=np.float32)
            
            # Create normal matrix (inverse transpose of model matrix)
            # For identity matrix, this is also identity
            normal_matrix = np.eye(3, dtype=np.float32)
            
            prog['mvp_matrix'].write(mvp_matrix.T.astype('f4').tobytes())
            prog['model_matrix'].write(model_matrix.T.astype('f4').tobytes())
            prog['normal_matrix'].write(normal_matrix.T.astype('f4').tobytes())
            
            # Light position (above and to the side of the model center)
            center_x = (self._bounds['min_x'] + self._bounds['max_x']) / 2.0
            center_y = (self._bounds['min_y'] + self._bounds['max_y']) / 2.0
            center_z = (self._bounds['min_z'] + self._bounds['max_z']) / 2.0
            
            # Calculate model dimensions
            model_width = self._bounds['max_x'] - self._bounds['min_x']
            model_height = self._bounds['max_y'] - self._bounds['min_y']
            model_depth = self._bounds['max_z'] - self._bounds['min_z']
            
            # Calculate model diagonal for overall size reference
            model_diagonal = np.sqrt(model_width**2 + model_height**2 + model_depth**2)
            
            # Position light at a distance proportional to model size
            # Use 2x the diagonal to ensure good coverage, with minimum distance for small models
            light_distance = max(model_diagonal * 2.0, 10.0)
            
            # Position light above and to the side for good lighting
            # Offset horizontally by 0.7x the distance for a nice angle
            horizontal_offset = light_distance * 0.7
            
            # Position light well above the model top for better top-down lighting
            light_height = self._bounds['max_z'] + light_distance * 1.5
            
            light_pos = np.array([
                center_x + horizontal_offset,
                center_y + horizontal_offset,
                light_height
            ], dtype=np.float32)

            prog['light_position'].write(light_pos.astype('f4').tobytes())
            prog['view_position'].write(camera_pos.astype('f4').tobytes())
            prog['light_color'].write(np.array([1.0, 1.0, 1.0], dtype=np.float32).tobytes())
            # Material properties - these control how the material responds to light
            prog['ambient_color'].write(np.array([0.3, 0.3, 0.3], dtype=np.float32).tobytes())
            prog['diffuse_color'].write(np.array([1.0, 1.0, 1.0], dtype=np.float32).tobytes())
            prog['specular_color'].write(np.array([1.0, 1.0, 1.0], dtype=np.float32).tobytes())
            prog['shininess'].write(np.array([64.0], dtype=np.float32).tobytes())

            frame_buffer.use()
            ctx.clear(depth=1.0)  # Clear depth buffer to maximum depth
            
            grid_vertex_array.render(mode=moderngl.LINES)
            vertex_array.render(mode=moderngl.TRIANGLES)

            ctx.copy_framebuffer(output, frame_buffer)
            img = Image.frombuffer('RGBA', output.size, output.read(components=4)).transpose(Image.Transpose.FLIP_TOP_BOTTOM)
            
            # Explicitly release OpenGL resources
            vertex_array.release()
            grid_vertex_array.release()
            vertex_buffer.release()
            grid_vertex_buffer.release()
            prog.release()
            output.release()
            frame_buffer.release()
            depth_buffer.release()
            
            return img
        finally:
            # Always release the context
            if ctx is not None:
                ctx.release()


def _process_segment_worker(segment_index, segment, width, height):
    """Worker function for processing a single segment in parallel."""
    vertices, normals = Renderer.get_segment_vertices(segment, width, height)
    return segment_index, vertices, normals
