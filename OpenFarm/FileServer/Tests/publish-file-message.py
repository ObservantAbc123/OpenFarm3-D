#!/usr/bin/env python3
import pika
import json
import sys
import os
import re
from datetime import datetime

# RabbitMQ connection parameters - must be set via environment variables
RABBITMQ_HOST = os.getenv('RABBITMQ_HOST')
RABBITMQ_USER = os.getenv('RABBITMQ_USER')
RABBITMQ_PASSWORD = os.getenv('RABBITMQ_PASSWORD')

if not all([RABBITMQ_HOST, RABBITMQ_USER, RABBITMQ_PASSWORD]):
    print("ERROR: Missing required environment variables")
    print("Required: RABBITMQ_HOST, RABBITMQ_USER, RABBITMQ_PASSWORD")
    sys.exit(1)

def get_download_type(file_type, url):
    if 'drive.google.com' in url:
        return 0  # GoogleDrive
    else:
        return 2  # Default to Test for other types

def send_file_message(file_url, print_job_id, file_type='gcode'):
    """Send a test message to RabbitMQ JobAccepted queue using AcceptMessage format"""

    credentials = pika.PlainCredentials(RABBITMQ_USER, RABBITMQ_PASSWORD)
    connection = pika.BlockingConnection(
        pika.ConnectionParameters(host=RABBITMQ_HOST, credentials=credentials)
    )

    channel = connection.channel()
    channel.queue_declare(queue='JobAccepted', durable=True)
    download_url = file_url
    download_type = get_download_type(file_type, download_url)

    message = {
        "JobId": print_job_id,
        "DownloadUrl": download_url,
        "DownloadType": download_type
    }

    # Publish to JobAccepted exchange
    channel.basic_publish(
        exchange='JobAccepted',
        routing_key='',
        body=json.dumps(message),
        properties=pika.BasicProperties(
            delivery_mode=2,  # Make message persistent
        )
    )

    print(f"Sent message to JobAccepted exchange for PrintJobID {print_job_id}:")
    print(f"  URL: {download_url}")
    print(f"  File Type: {file_type}")
    print(f"  Download Type: {download_type} ({get_download_type_name(download_type)})")
    print(f"  Message: {json.dumps(message, indent=2)}")

    connection.close()

def get_download_type_name(download_type):
    type_names = {
        0: "GoogleDrive",
        1: "Jira",
        2: "Test"
    }
    return type_names.get(download_type, "Unknown")

if __name__ == "__main__":
    if len(sys.argv) < 4:
        print("ERROR: Missing required arguments")
        print("Usage: python3 publish-file-message.py <file_url> <file_type> <print_job_id>")
        print("  file_url: URL of the file to download")
        print("  file_type: gcode, test, or image")
        print("  print_job_id: integer (required)")
        print("")
        print("Examples:")
        print("  python3 publish-file-message.py https://example.com/test.gcode gcode 12345")
        print("  python3 publish-file-message.py 'https://drive.google.com/uc?export=download&id=FILE_ID' gcode 67890")
        print("  python3 publish-file-message.py https://example.com/image.png image 99999")
        print("")
        print("Download Types (auto-detected):")
        print("  - Google Drive URLs → GoogleDrive (0)")
        print("  - Other URLs with 'gcode' or 'test' → Test (2)")
        print("")
        print("Note: Use direct download URLs for Google Drive files")
        sys.exit(1)

    file_url = sys.argv[1]
    file_type = sys.argv[2]
    print_job_id = int(sys.argv[3])

    send_file_message(file_url, print_job_id, file_type)
