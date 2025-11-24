from Renderer import Renderer
from PIL import Image
import os
import json
import pika
import requests
import tempfile
import signal
import sys
from pathlib import Path
import logging
import psycopg2
from psycopg2.extras import RealDictCursor
from urllib.parse import urlparse
import gc
import threading
import queue

# Configure logging
logging.basicConfig(
    level=logging.INFO,
    format='%(asctime)s - %(name)s - %(levelname)s - %(message)s'
)
logger = logging.getLogger(__name__)

# Configuration from environment variables
RABBITMQ_USER = os.getenv('RABBITMQ_USER', 'guest')
RABBITMQ_PASSWORD = os.getenv('RABBITMQ_PASSWORD', 'guest')
RABBITMQ_HOST = os.getenv('RABBITMQ_HOST', 'localhost')
FILE_SERVER_BASE_URL = os.getenv('FILE_SERVER_BASE_URL', 'http://file-processor:80')
DATABASE_CONNECTION_STRING = os.getenv('DATABASE_CONNECTION_STRING')
QUEUE_NAME = 'GcodeRendererJobValidated'

# Default printer configuration (fallback if database query fails)
DEFAULT_PRINTER_INFO = {
    'bed_min_x': 0,
    'bed_min_y': 0,
    'bed_min_z': 0,
    'bed_max_x': 250,
    'bed_max_y': 210,
    'bed_max_z': 220
}

width = 3840
height = 2160
views = ["NORTH_WEST", "WEST", "SOUTH_WEST", "SOUTH", "SOUTH_EAST", "EAST", "NORTH_EAST", "NORTH"]

# Global connection and channel
connection = None
channel = None
should_stop = False
consumer_tag = None
processing_message = False  # Flag to track if we're currently processing a message


def signal_handler(sig, frame):
    """Handle shutdown signals gracefully"""
    global should_stop, channel, connection, processing_message
    logger.info("Received shutdown signal, stopping service...")
    should_stop = True
    # Stop consuming first - this will cause start_consuming() to return
    # But don't close connection if we're processing a message - let it finish
    if channel and not channel.is_closed:
        try:
            channel.stop_consuming()
            logger.info("Stopped consuming")
        except Exception as e:
            logger.warning(f"Error stopping consumer: {e}")
    # Only close connection if we're not processing a message
    # The connection will be closed in the finally block after message processing
    if not processing_message and connection and not connection.is_closed:
        try:
            connection.close()
            logger.info("Closed connection in signal handler")
        except Exception as e:
            logger.warning(f"Error closing connection: {e}")
    elif processing_message:
        logger.info("Message processing in progress, will close connection after completion")


def connect_rabbitmq():
    """Connect to RabbitMQ with retry logic"""
    global connection, channel, processing_message
    
    # Don't reconnect if we're currently processing a message
    if processing_message:
        logger.warning("Cannot reconnect while processing a message. Waiting for message to complete...")
        import time
        wait_time = 0
        while processing_message and wait_time < 300:  # Wait up to 5 minutes
            time.sleep(1)
            wait_time += 1
        if processing_message:
            logger.error("Message processing did not complete in time. Forcing reconnection.")
    
    # Close existing connection if it exists
    if connection and not connection.is_closed:
        try:
            logger.info("Closing existing connection before reconnecting...")
            connection.close()
        except Exception as e:
            logger.warning(f"Error closing existing connection: {e}")
    
    credentials = pika.PlainCredentials(RABBITMQ_USER, RABBITMQ_PASSWORD)
    parameters = pika.ConnectionParameters(
        host=RABBITMQ_HOST,
        port=5672,
        credentials=credentials,
        heartbeat=1800,  # 30 minute heartbeat (1800 seconds) - increased for long renders
        blocked_connection_timeout=300,
        # Add connection attempt timeout
        connection_attempts=3,
        retry_delay=2,
        # Socket timeout - how long to wait for socket operations
        # Set higher than heartbeat to prevent premature timeouts
        socket_timeout=2000  # ~33 minutes, higher than heartbeat
    )
    
    max_retries = 5
    retry_count = 0
    
    while retry_count < max_retries and not should_stop:
        try:
            logger.info(f"Attempting to connect to RabbitMQ at {RABBITMQ_HOST}... (attempt {retry_count + 1}/{max_retries})")
            connection = pika.BlockingConnection(parameters)
            channel = connection.channel()
            
            # Log connection details for debugging
            logger.info(f"Connection established. Connection state: is_closed={connection.is_closed}")
            logger.info(f"Connection parameters: heartbeat={parameters.heartbeat}s, socket_timeout={getattr(parameters, 'socket_timeout', 'default')}s")
            
            # Declare queue to ensure it exists
            channel.queue_declare(queue=QUEUE_NAME, durable=True)
            
            # Set QoS to process one message at a time
            channel.basic_qos(prefetch_count=1)
            
            logger.info(f"Successfully connected to RabbitMQ and declared queue '{QUEUE_NAME}'")
            return True
        except Exception as e:
            retry_count += 1
            logger.error(f"Failed to connect to RabbitMQ (attempt {retry_count}/{max_retries}): {e}")
            if retry_count < max_retries:
                import time
                time.sleep(5)
            else:
                logger.error("Max retries reached. Exiting.")
                return False
    
    return False


def get_printer_info_from_db(job_id):
    """Get printer model info from database for the given job_id"""
    if not DATABASE_CONNECTION_STRING:
        logger.warning("DATABASE_CONNECTION_STRING not set, using default printer info")
        return DEFAULT_PRINTER_INFO
    
    try:
        # Try to parse connection string - handle both URL and key=value formats
        parsed = urlparse(DATABASE_CONNECTION_STRING)
        
        # If it's a URL format (postgresql://user:password@host:port/database)
        if parsed.scheme in ('postgresql', 'postgres'):
            if not parsed.hostname:
                logger.error("Database host not found in connection string URL")
                return DEFAULT_PRINTER_INFO
            
            connect_kwargs = {
                'host': parsed.hostname,
                'port': parsed.port or 5432,
                'dbname': parsed.path.lstrip('/'),  # psycopg2 uses 'dbname' not 'database'
                'user': parsed.username,
                'password': parsed.password
            }
        else:
            # Assume it's in Npgsql format (Host=...;Port=...;Database=...;Username=...;Password=...)
            # Parse key-value pairs
            conn_params = {}
            for part in DATABASE_CONNECTION_STRING.split(';'):
                part = part.strip()
                if not part:  # Skip empty parts (e.g., trailing semicolon)
                    continue
                if '=' in part:
                    key, value = part.split('=', 1)
                    key = key.strip().lower()
                    value = value.strip()
                    if key == 'host' or key == 'server':
                        conn_params['host'] = value
                    elif key == 'port':
                        try:
                            conn_params['port'] = int(value)
                        except ValueError:
                            logger.warning(f"Invalid port value: {value}, using default 5432")
                            conn_params['port'] = 5432
                    elif key == 'database' or key == 'db':
                        conn_params['database'] = value
                    elif key == 'username' or key == 'user' or key == 'uid':
                        conn_params['user'] = value
                    elif key == 'password' or key == 'pwd':
                        conn_params['password'] = value
            
            # Ensure host is set (required to force TCP connection, not Unix socket)
            if 'host' not in conn_params or not conn_params['host']:
                logger.error(f"Database host not found in connection string. Available keys: {list(conn_params.keys())}")
                logger.error(f"Connection string format: {DATABASE_CONNECTION_STRING[:200]}")
                return DEFAULT_PRINTER_INFO
            
            # Build connection parameters - all required fields
            connect_kwargs = {
                'host': conn_params['host'],
                'port': conn_params.get('port', 5432),
            }
            
            # Add optional parameters if present
            if conn_params.get('database'):
                connect_kwargs['dbname'] = conn_params['database']  # psycopg2 uses 'dbname' not 'database'
            if conn_params.get('user'):
                connect_kwargs['user'] = conn_params['user']
            if conn_params.get('password'):
                connect_kwargs['password'] = conn_params['password']
            
            logger.debug(f"Parsed connection params: host={connect_kwargs.get('host')}, port={connect_kwargs.get('port')}, dbname={connect_kwargs.get('dbname')}, user={connect_kwargs.get('user')}")
        
        logger.info(f"Connecting to PostgreSQL at {connect_kwargs['host']}:{connect_kwargs.get('port', 5432)}")
        
        # Connect to database using TCP (not Unix socket)
        # Explicitly set host to force TCP connection
        conn = psycopg2.connect(**connect_kwargs)
        
        try:
            with conn.cursor(cursor_factory=RealDictCursor) as cur:
                # Query to get printer model info for the job
                query = """
                    SELECT 
                        pm.bed_x_min, pm.bed_x_max,
                        pm.bed_y_min, pm.bed_y_max,
                        pm.bed_z_min, pm.bed_z_max
                    FROM print_jobs pj
                    JOIN printer_models pm ON pj.printer_model_id = pm.id
                    WHERE pj.id = %s
                """
                cur.execute(query, (job_id,))
                result = cur.fetchone()
                
                if result:
                    printer_info = {
                        'bed_min_x': result['bed_x_min'] or 0,
                        'bed_min_y': result['bed_y_min'] or 0,
                        'bed_min_z': result['bed_z_min'] or 0,
                        'bed_max_x': result['bed_x_max'] or 250,
                        'bed_max_y': result['bed_y_max'] or 210,
                        'bed_max_z': result['bed_z_max'] or 220
                    }
                    logger.info(f"Retrieved printer info from database for job {job_id}: {printer_info}")
                    return printer_info
                else:
                    logger.warning(f"No printer model found for job {job_id}, using default printer info")
                    return DEFAULT_PRINTER_INFO
        finally:
            conn.close()
    except Exception as e:
        logger.error(f"Error fetching printer info from database for job {job_id}: {e}", exc_info=True)
        return DEFAULT_PRINTER_INFO


def download_gcode_file(job_id):
    """Download gcode file from file server"""
    url = f"{FILE_SERVER_BASE_URL}/api/gcode/{job_id}/bytes"
    response = None
    temp_file = None
    
    try:
        logger.info(f"Downloading gcode file for job {job_id} from {url}")
        response = requests.get(url, timeout=300)  # 5 minute timeout for large files
        response.raise_for_status()
        
        # Create temporary file to store gcode
        temp_file = tempfile.NamedTemporaryFile(mode='wb', suffix='.gcode', delete=False)
        temp_file.write(response.content)
        temp_file.close()
        
        logger.info(f"Downloaded gcode file for job {job_id}, size: {len(response.content)} bytes")
        return temp_file.name
    except requests.exceptions.RequestException as e:
        logger.error(f"Failed to download gcode file for job {job_id}: {e}")
        return None
    finally:
        # Close response to free memory
        if response is not None:
            response.close()


def upload_png_to_fileserver(job_id, png_bytes, view_index, view_name):
    """Upload PNG file to fileserver images endpoint with view-specific filename"""
    url = f"{FILE_SERVER_BASE_URL}/api/images/{job_id}"
    response = None
    
    # Create filename with view information: job_{job_id}_view_{index}_{view_name}.png
    filename = f"job_{job_id}_view_{view_index}_{view_name.lower()}.png"
    
    try:
        logger.info(f"Uploading PNG file {filename} for job {job_id} to {url}")
        files = {
            'file': (filename, png_bytes, 'image/png')
        }
        response = requests.post(url, files=files, timeout=300)  # 5 minute timeout for large files
        response.raise_for_status()
        
        logger.info(f"Successfully uploaded PNG file {filename} for job {job_id}")
        return True
    except requests.exceptions.RequestException as e:
        logger.error(f"Failed to upload PNG file {filename} for job {job_id}: {e}")
        return False
    finally:
        # Close response to free memory
        if response is not None:
            response.close()


def render_gcode(gcode_path, job_id, printer_info):
    """Render gcode file and upload 8 PNG files (one per view) to fileserver"""
    global connection
    renderer = None
    try:
        logger.info(f"Starting render for job {job_id}")
        
        # Create renderer
        renderer = Renderer(gcode_path, printer_info)
        
        # Render all views and upload each as a PNG
        upload_success_count = 0
        for view_index, view in enumerate(views):
            # NOTE: We do NOT call process_data_events() here because:
            # 1. It can interfere with start_consuming() and cause it to exit
            # 2. BlockingConnection should handle heartbeats automatically in the background
            # 3. The heartbeat is set to 600 seconds (10 minutes), which should be enough
            #    for most rendering operations
            # If rendering takes longer than 10 minutes, we'll need a different approach
            # (e.g., threading or async connection)
            
            img = None
            try:
                logger.info(f"Rendering view {view} (index {view_index}) for job {job_id}")
                img = renderer.render_view(view, width, height)
                
                # Create black background and composite the image onto it
                black_bg = Image.new('RGB', img.size, (0, 0, 0))
                # Composite the RGBA image onto the black background
                black_bg.paste(img, mask=img.split()[3] if img.mode == 'RGBA' else None)
                
                # Convert image to PNG bytes
                import io
                png_buffer = io.BytesIO()
                black_bg.save(png_buffer, format='PNG')
                png_bytes = png_buffer.getvalue()
                png_buffer.close()
                
                logger.info(f"Created PNG for view {view} (index {view_index}), size: {len(png_bytes)} bytes")
                
                # Upload PNG to fileserver
                if upload_png_to_fileserver(job_id, png_bytes, view_index, view):
                    upload_success_count += 1
                    logger.info(f"Successfully uploaded PNG for view {view} (index {view_index})")
                else:
                    logger.error(f"Failed to upload PNG for view {view} (index {view_index})")
                
                # Close the composite image to free memory immediately
                black_bg.close()
                
            except Exception as e:
                logger.error(f"Error rendering/uploading view {view} (index {view_index}) for job {job_id}: {e}", exc_info=True)
            finally:
                # Close the original rendered image to free memory
                if img is not None:
                    img.close()
        
        # Check if all uploads were successful
        if upload_success_count == len(views):
            logger.info(f"Completed render and upload for job {job_id}: all {len(views)} PNG files uploaded")
            return True
        else:
            logger.warning(f"Partial success for job {job_id}: {upload_success_count}/{len(views)} PNG files uploaded")
            return upload_success_count > 0  # Return True if at least one succeeded
        
    except Exception as e:
        logger.error(f"Error rendering gcode for job {job_id}: {e}", exc_info=True)
        return False
    finally:
        # Clean up renderer (it will be garbage collected, but explicit cleanup helps)
        renderer = None


def safe_ack(ch, delivery_tag):
    """Safely acknowledge a message, handling closed connections"""
    try:
        if ch and not ch.is_closed:
            ch.basic_ack(delivery_tag=delivery_tag)
        else:
            logger.warning(f"Channel is closed, cannot ack message with delivery_tag {delivery_tag}")
    except Exception as e:
        logger.error(f"Error acknowledging message with delivery_tag {delivery_tag}: {e}")


def safe_nack(ch, delivery_tag, requeue=True):
    """Safely nack a message, handling closed connections"""
    try:
        if ch and not ch.is_closed:
            ch.basic_nack(delivery_tag=delivery_tag, requeue=requeue)
        else:
            logger.warning(f"Channel is closed, cannot nack message with delivery_tag {delivery_tag}")
    except Exception as e:
        logger.error(f"Error nacking message with delivery_tag {delivery_tag}: {e}")


def process_message(ch, method, properties, body):
    """Process a message from RabbitMQ"""
    global processing_message, connection, channel
    processing_message = True
    delivery_tag = None
    job_id = None
    
    try:
        # Store delivery tag for later use
        delivery_tag = method.delivery_tag
        
        # Parse message
        message = json.loads(body.decode('utf-8'))
        job_id = message.get('JobId')
        
        if not job_id:
            logger.error("Message missing JobId field")
            safe_nack(ch, method.delivery_tag, requeue=False)
            return
        
        logger.info(f"Received message for job {job_id} (delivery_tag: {delivery_tag})")
        
        # Check if connection is still valid before starting processing
        if not connection or connection.is_closed or not channel or channel.is_closed:
            logger.error(f"Connection/channel is closed before processing job {job_id}. Message will be redelivered.")
            # Can't nack here because channel is closed, message will be redelivered
            return
        
        # Get printer info from database
        printer_info = get_printer_info_from_db(job_id)
        
        # Download gcode file
        gcode_path = download_gcode_file(job_id)
        if not gcode_path:
            logger.error(f"Failed to download gcode file for job {job_id}")
            safe_nack(ch, method.delivery_tag, requeue=True)
            return
        
        try:
            # Check connection again before rendering (rendering can take a long time)
            if not connection or connection.is_closed or not channel or channel.is_closed:
                logger.error(f"Connection/channel closed before rendering job {job_id}. Message will be redelivered.")
                return
            
            # Render gcode
            success = render_gcode(gcode_path, job_id, printer_info)
            
            # Check connection again after rendering (connection might have closed during rendering)
            if not connection or connection.is_closed or not channel or channel.is_closed:
                logger.error(f"Connection/channel closed after rendering job {job_id}. Message will be redelivered.")
                logger.info(f"Job {job_id} processing completed but cannot acknowledge due to closed connection.")
                return
            
            if success:
                logger.info(f"Successfully processed job {job_id}")
                safe_ack(ch, method.delivery_tag)
            else:
                logger.error(f"Failed to render gcode for job {job_id}")
                safe_nack(ch, method.delivery_tag, requeue=True)
        finally:
            # Clean up temporary gcode file
            try:
                os.unlink(gcode_path)
            except Exception as e:
                logger.warning(f"Failed to delete temporary file {gcode_path}: {e}")
            
            # Force garbage collection to free memory after processing each message
            gc.collect()
                
    except json.JSONDecodeError as e:
        logger.error(f"Failed to parse message: {e}")
        safe_nack(ch, method.delivery_tag, requeue=False)
    except Exception as e:
        logger.error(f"Error processing message: {e}", exc_info=True)
        safe_nack(ch, method.delivery_tag, requeue=True)
    finally:
        processing_message = False
        logger.info(f"Finished processing message for job {job_id if 'job_id' in locals() else 'unknown'}. Consumer should continue waiting for more messages.")
        # If we received a shutdown signal and finished processing, close connection
        if should_stop and connection and not connection.is_closed:
            try:
                logger.info("Message processing complete, closing connection due to shutdown signal")
                connection.close()
            except Exception as e:
                logger.warning(f"Error closing connection after message processing: {e}")


def start_consumer():
    """Start consuming messages from RabbitMQ"""
    global connection, channel, should_stop, consumer_tag
    
    # Set up signal handlers
    signal.signal(signal.SIGINT, signal_handler)
    signal.signal(signal.SIGTERM, signal_handler)
    
    # Connect to RabbitMQ
    if not connect_rabbitmq():
        logger.error("Failed to connect to RabbitMQ. Exiting.")
        sys.exit(1)
    
    # Set up consumer
    try:
        logger.info(f"GcodeOracle service started. Waiting for messages on queue '{QUEUE_NAME}'...")
        logger.info(f"File server URL: {FILE_SERVER_BASE_URL}")
        
        # Start consuming (blocking call)
        while not should_stop:
            try:
                # Don't start a new consumer if we're processing a message
                if processing_message:
                    logger.warning("Cannot start new consumer while processing a message. Waiting...")
                    import time
                    wait_time = 0
                    while processing_message and wait_time < 300:  # Wait up to 5 minutes
                        time.sleep(1)
                        wait_time += 1
                    if processing_message:
                        logger.error("Message processing did not complete in time. Exiting.")
                        break
                
                # Set up consumer
                consumer_tag = channel.basic_consume(
                    queue=QUEUE_NAME,
                    on_message_callback=process_message,
                    auto_ack=False
                )
                
                logger.info(f"Started consuming from queue '{QUEUE_NAME}' with consumer tag '{consumer_tag}'")
                
                # Start consuming - this is a blocking call
                # It will return when stop_consuming() is called or connection is closed
                logger.info("Starting to consume messages (blocking call - will wait for messages indefinitely)...")
                channel.start_consuming()
                logger.info("start_consuming() has returned (this should only happen if consumer was stopped or connection closed)")
                
                # If we get here, consuming was stopped
                logger.info("start_consuming() returned. Checking if we should continue...")
                # Check if we should exit or reconnect
                if should_stop:
                    logger.info("should_stop is True, breaking out of consumer loop")
                    break
                
                # If we get here, consuming stopped but we should continue
                # This might happen if the connection was closed or consumer was cancelled
                logger.warning("start_consuming() returned unexpectedly. Checking connection state...")
                
                # Check connection state
                if not connection or connection.is_closed:
                    logger.warning("Connection is closed. Will attempt to reconnect and restart consumer.")
                    # The exception handler below will catch this and reconnect
                    raise pika.exceptions.ConnectionClosedByBroker("Connection closed during consuming")
                elif not channel or channel.is_closed:
                    logger.warning("Channel is closed. Will attempt to reconnect and restart consumer.")
                    raise pika.exceptions.AMQPConnectionError("Channel closed during consuming")
                else:
                    logger.warning("Connection and channel appear valid. Restarting consumer...")
                    # Connection is still valid, just restart consuming
                    continue
                
            except pika.exceptions.ConnectionClosedByBroker:
                if not should_stop:
                    # Wait for any in-progress message to finish before reconnecting
                    if processing_message:
                        logger.warning("Connection closed by broker while processing message. Waiting for message to complete...")
                        import time
                        # Wait up to 5 minutes for message processing to complete
                        wait_time = 0
                        while processing_message and wait_time < 300:
                            time.sleep(1)
                            wait_time += 1
                        if processing_message:
                            logger.error("Message processing did not complete in time. Proceeding with reconnection.")
                    
                    logger.warning("Connection closed by broker, attempting to reconnect...")
                    if connect_rabbitmq():
                        continue
                    else:
                        logger.error("Failed to reconnect. Exiting.")
                        break
            except pika.exceptions.AMQPConnectionError:
                if not should_stop:
                    # Wait for any in-progress message to finish before reconnecting
                    if processing_message:
                        logger.warning("AMQP connection error while processing message. Waiting for message to complete...")
                        import time
                        # Wait up to 5 minutes for message processing to complete
                        wait_time = 0
                        while processing_message and wait_time < 300:
                            time.sleep(1)
                            wait_time += 1
                        if processing_message:
                            logger.error("Message processing did not complete in time. Proceeding with reconnection.")
                    
                    logger.warning("AMQP connection error, attempting to reconnect...")
                    if connect_rabbitmq():
                        continue
                    else:
                        logger.error("Failed to reconnect. Exiting.")
                        break
            except Exception as e:
                if not should_stop:
                    logger.error(f"Error in consumer: {e}", exc_info=True)
                    # Wait for any in-progress message to finish before reconnecting
                    if processing_message:
                        logger.warning("Error occurred while processing message. Waiting for message to complete...")
                        import time
                        # Wait up to 5 minutes for message processing to complete
                        wait_time = 0
                        while processing_message and wait_time < 300:
                            time.sleep(1)
                            wait_time += 1
                        if processing_message:
                            logger.error("Message processing did not complete in time. Proceeding with reconnection.")
                    
                    # Try to reconnect
                    if not connection or connection.is_closed:
                        logger.info("Connection lost, attempting to reconnect...")
                        if connect_rabbitmq():
                            continue
                        else:
                            logger.error("Failed to reconnect. Exiting.")
                            break
                else:
                    # If we're stopping, break out of the loop
                    break
    except KeyboardInterrupt:
        logger.info("Received keyboard interrupt")
    except Exception as e:
        logger.error(f"Error in consumer: {e}", exc_info=True)
    finally:
        # Stop consuming if still active
        if channel and not channel.is_closed:
            try:
                channel.stop_consuming()
            except Exception as e:
                logger.warning(f"Error stopping consumer: {e}")
        if connection and not connection.is_closed:
            logger.info("Closing RabbitMQ connection...")
            try:
                connection.close()
            except Exception as e:
                logger.warning(f"Error closing connection: {e}")
        logger.info("GcodeOracle service stopped")


if __name__ == "__main__":
    start_consumer()
