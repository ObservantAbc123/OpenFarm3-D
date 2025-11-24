import sys
import math

class GcodeManager:
    def __init__(self, gcode_path):
        self.gcode_path = gcode_path
        self.nozzle_x = 0
        self.nozzle_y = 0
        self.nozzle_z = 0
        self.absolute_movement = True
        self.segments = []
        self.parse_gcode()

    # Updates position based on command values.
    # Returns 2 tuples representing the previous position and the new position if the command was an extrusion command
    def update_position(self, command):
        old_x = self.nozzle_x
        old_y = self.nozzle_y
        old_z = self.nozzle_z

        if self.absolute_movement:
            if command.X:
                self.nozzle_x = command.X if command.X is not None else self.nozzle_x
            if command.Y:
                self.nozzle_y = command.Y if command.Y is not None else self.nozzle_y
            if command.Z:
                self.nozzle_z = command.Z if command.Z is not None else self.nozzle_z
        else:
            if command.X:
                self.nozzle_x += command.X
            if command.Y:
                self.nozzle_y += command.Y
            if command.Z:
                self.nozzle_z += command.Z

        # If nozzle position is negative this is likely a purge line and we don't want to render it.
        if self.nozzle_x < 0 or self.nozzle_y < 0 or self.nozzle_z < 0:
            return None, None
        # If there is positive extrusion and movement commanded, this is an extrusion line we want to render.
        if command.E is not None and command.E  > 0 and (command.X is not None or command.Y is not None or command.Z is not None):
            return (old_x, old_y, old_z), (self.nozzle_x, self.nozzle_y, self.nozzle_z)

        return None, None

    def process_arc(self, command, clockwise):
        """
        Process G2 (clockwise) or G3 (counter-clockwise) arc commands.
        Converts arcs into line segments for rendering.
        Returns list of (old_pos, new_pos) tuples for segments with extrusion.
        """
        old_x = self.nozzle_x
        old_y = self.nozzle_y
        old_z = self.nozzle_z
        
        # Calculate end position
        if self.absolute_movement:
            end_x = command.X if command.X is not None else self.nozzle_x
            end_y = command.Y if command.Y is not None else self.nozzle_y
            end_z = command.Z if command.Z is not None else self.nozzle_z
        else:
            end_x = self.nozzle_x + (command.X if command.X is not None else 0)
            end_y = self.nozzle_y + (command.Y if command.Y is not None else 0)
            end_z = self.nozzle_z + (command.Z if command.Z is not None else 0)
        
        # If nozzle position is negative, skip rendering
        if self.nozzle_x < 0 or self.nozzle_y < 0 or self.nozzle_z < 0:
            self.nozzle_x = end_x
            self.nozzle_y = end_y
            self.nozzle_z = end_z
            return []
        
        # Calculate arc center from I, J, K offsets
        center_x = self.nozzle_x + (command.I if command.I is not None else 0)
        center_y = self.nozzle_y + (command.J if command.J is not None else 0)
        center_z = self.nozzle_z + (command.K if command.K is not None else 0)
        
        # Calculate radius from center to start position
        start_vec_x = self.nozzle_x - center_x
        start_vec_y = self.nozzle_y - center_y
        radius = math.sqrt(start_vec_x**2 + start_vec_y**2)
        
        # If radius is too small, treat as a straight line
        if radius < 0.001:
            self.nozzle_x = end_x
            self.nozzle_y = end_y
            self.nozzle_z = end_z
            if command.E is not None and command.E > 0:
                return [((old_x, old_y, old_z), (end_x, end_y, end_z))]
            return []
        
        # Calculate start and end angles
        start_angle = math.atan2(start_vec_y, start_vec_x)
        end_vec_x = end_x - center_x
        end_vec_y = end_y - center_y
        end_angle = math.atan2(end_vec_y, end_vec_x)
        
        # Calculate angular span
        angle_diff = end_angle - start_angle
        
        # Normalize angle difference for clockwise/counter-clockwise
        if clockwise:  # G2
            if angle_diff > 0:
                angle_diff -= 2 * math.pi
        else:  # G3
            if angle_diff < 0:
                angle_diff += 2 * math.pi
        
        # Subdivide arc into segments (use adaptive subdivision based on arc length)
        arc_length = abs(angle_diff) * radius
        # Use approximately 1mm segments, but at least 4 segments per arc
        num_segments = max(4, int(arc_length / 1.0))
        
        segments = []
        z_step = (end_z - self.nozzle_z) / num_segments if num_segments > 0 else 0
        
        # Only create segments if there's extrusion
        if command.E is not None and command.E > 0:
            # Start from the current position
            prev_x, prev_y, prev_z = old_x, old_y, old_z
            
            # Create segments along the arc
            for i in range(1, num_segments + 1):
                t = i / num_segments
                angle = start_angle + angle_diff * t
                
                # For the last segment, use the exact end position
                if i == num_segments:
                    seg_x, seg_y, seg_z = end_x, end_y, end_z
                else:
                    seg_x = center_x + radius * math.cos(angle)
                    seg_y = center_y + radius * math.sin(angle)
                    seg_z = self.nozzle_z + z_step * i
                
                segments.append(((prev_x, prev_y, prev_z), (seg_x, seg_y, seg_z)))
                prev_x, prev_y, prev_z = seg_x, seg_y, seg_z
        
        # Update nozzle position
        self.nozzle_x = end_x
        self.nozzle_y = end_y
        self.nozzle_z = end_z
        
        return segments

    def parse_gcode(self):
        # Read G-code file
        try:
            with open(self.gcode_path, 'r', encoding='utf-8') as f:
                gcode_content = f.read()
        except Exception as e:
            print(f"Error reading G-code file: {e}")
            sys.exit(1)

        for line in gcode_content.splitlines():
            if len(line) == 0 or line[0] == ';':
                continue

            command = GcodeCommand(line)
            old_pos, new_pos = None,  None
            # Skip comments
            match command.type:
                case ';':
                    continue
                case 'G90': # Set absolute movement : https://marlinfw.org/docs/gcode/G090.html
                    self.absolute_movement = True
                case 'G91': # Set relative movement : https://marlinfw.org/docs/gcode/G091.html
                    self.absolute_movement = False
                case 'G92': # Set position : https://marlinfw.org/docs/gcode/G092.html
                    continue # TODO: Anything for G92? Not sure it's needed. Seems to only be used for extruder
                case 'G0': # Linear move : https://marlinfw.org/docs/gcode/G000-G001.html
                    old_pos, new_pos = self.update_position(command)
                case 'G1': # Linear move : https://marlinfw.org/docs/gcode/G000-G001.html
                    old_pos, new_pos = self.update_position(command)
                case 'G2': # Clockwise arc : https://marlinfw.org/docs/gcode/G002-G003.html
                    arc_segments = self.process_arc(command, clockwise=True)
                    for seg_start, seg_end in arc_segments:
                        self.segments.append(Segment(seg_start, seg_end))
                case 'G3': # Counter-clockwise arc : https://marlinfw.org/docs/gcode/G002-G003.html
                    arc_segments = self.process_arc(command, clockwise=False)
                    for seg_start, seg_end in arc_segments:
                        self.segments.append(Segment(seg_start, seg_end))

            if old_pos is not None and new_pos is not None:
                self.segments.append(Segment(old_pos, new_pos))

    def get_bounds(self):
        if not self.segments:
            return None

        all_x = []
        all_y = []
        all_z = []

        for segment in self.segments:
            all_x.append(segment.start[0])
            all_y.append(segment.start[1])
            all_z.append(segment.start[2])
            all_x.append(segment.end[0])
            all_y.append(segment.end[1])
            all_z.append(segment.end[2])

        bounds = {
            'min_x': min(all_x), 'max_x': max(all_x),
            'min_y': min(all_y), 'max_y': max(all_y),
            'min_z': min(all_z), 'max_z': max(all_z)
        }
        return bounds


class GcodeCommand:
    def __init__(self, command_string):
        self.X = None
        self.Y = None
        self.Z = None
        self.E = None
        self.F = None
        self.I = None  # Arc center X offset
        self.J = None  # Arc center Y offset
        self.K = None  # Arc center Z offset
        self.R = None  # Arc radius (alternative to I/J/K)
        split = command_string.split()
        self.type = split[0]
        for param in split[1:]:
            if param.startswith(';'):
                break
            elif param.startswith('X'):
                self.X = float(param[1:]) if len(param) > 1 else None
            elif param.startswith('Y'):
                self.Y = float(param[1:]) if len(param) > 1 else None
            elif param.startswith('Z'):
                self.Z = float(param[1:]) if len(param) > 1 else None
            elif param.startswith('E'):
                self.E = float(param[1:]) if len(param) > 1 else None
            elif param.startswith('F'):
                self.F = float(param[1:]) if len(param) > 1 else None
            elif param.startswith('I'):
                self.I = float(param[1:]) if len(param) > 1 else None
            elif param.startswith('J'):
                self.J = float(param[1:]) if len(param) > 1 else None
            elif param.startswith('K'):
                self.K = float(param[1:]) if len(param) > 1 else None
            elif param.startswith('R'):
                self.R = float(param[1:]) if len(param) > 1 else None

class Segment:
    def __init__(self, start, end):
        self.start = start
        self.end = end