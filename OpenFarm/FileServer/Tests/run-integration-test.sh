#!/bin/bash

set -e

echo "=== FileServer Test Suite ==="
echo ""

# Colors for output
GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
NC='\033[0m'

# Get the absolute path to this script's directory
SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"

# Configuration variables
TEMP_PORT=${TEMP_PORT:-8888}
DOCKER_NETWORK=${DOCKER_NETWORK:-openfarm_file-processor-network}
PYTHON_IMAGE=${PYTHON_IMAGE:-python:3.9-slim}
EXTERNAL_TEST_URL=${EXTERNAL_TEST_URL:-"https://raw.githubusercontent.com/JoshuaSchell/Prusa3D-Sample-Objects/master/1.75mm/ABS_Batman_200um_30M.gcode"}
GOOGLE_DRIVE_TEST_URL=${GOOGLE_DRIVE_TEST_URL:-"https://drive.google.com/uc?export=download&id=13Nzp3U4t6-HJMG-c5DEWA2pnT6Ios0Q4"}
PROCESSING_TIMEOUT_SHORT=${PROCESSING_TIMEOUT_SHORT:-5}
PROCESSING_TIMEOUT_LONG=${PROCESSING_TIMEOUT_LONG:-10}
MINIO_ALIAS=${MINIO_ALIAS:-local}

# Find and load .env file by traversing up from script location
find_env_file() {
    local current_dir="$SCRIPT_DIR"

    # Traverse up the directory tree looking for .env
    while [ "$current_dir" != "/" ]; do
        if [ -f "$current_dir/.env" ]; then
            echo "$current_dir/.env"
            return 0
        fi
        current_dir="$(dirname "$current_dir")"
    done

    return 1
}

# Load environment variables from .env file
ENV_FILE=$(find_env_file)
if [ -n "$ENV_FILE" ]; then
    set -a
    source "$ENV_FILE"
    set +a
    echo -e "${GREEN}Loaded environment from: $ENV_FILE${NC}"
else
    echo -e "${YELLOW}Warning: .env file not found in any parent directory${NC}"
fi

# Check required environment variables
check_env_vars() {
    local required_vars=("RABBITMQ_HOST" "RABBITMQ_USER" "RABBITMQ_PASSWORD" "MINIO_ROOT_USER" "MINIO_ROOT_PASSWORD")
    local missing_vars=()

    for var in "${required_vars[@]}"; do
        if [ -z "${!var}" ]; then
            missing_vars+=("$var")
        fi
    done

    if [ ${#missing_vars[@]} -ne 0 ]; then
        echo -e "${RED}ERROR: Missing required environment variables:${NC}"
        for var in "${missing_vars[@]}"; do
            echo -e "${RED}  - $var${NC}"
        done
        echo -e "${RED}Please ensure .env file exists and contains all required variables${NC}"
        exit 1
    fi
}

FILE_SERVER_URL=${FILE_SERVER_URL:-http://localhost:5001}

# Check if docker-compose is running
check_services() {
    echo -e "${YELLOW}Checking Docker services...${NC}"

    local services=("rabbitmq" "minio-server" "file-processor-service")
    local missing_services=()

    for service in "${services[@]}"; do
        if ! docker ps | grep -q "$service"; then
            missing_services+=("$service")
        fi
    done

    if [ ${#missing_services[@]} -ne 0 ]; then
        echo -e "${RED}The following services are not running: ${missing_services[*]}${NC}"
        echo "Please run: docker-compose up -d"
        exit 1
    fi

    echo -e "${GREEN}All required services are running!${NC}"
}

# Send message to RabbitMQ using updated message format for JobAccepted queue
send_rabbitmq_message() {
    local url=$1
    local file_type=$2
    local print_job_id=$3

    echo -e "${YELLOW}Sending RabbitMQ message to JobAccepted queue${NC}"

    if [ -z "$print_job_id" ]; then
        echo -e "${RED}ERROR: Print job ID is required${NC}"
        return 1
    fi

    docker run --rm \
        --network "$DOCKER_NETWORK" \
        -v "$SCRIPT_DIR/publish-file-message.py:/publish-file-message.py" \
        "$PYTHON_IMAGE" bash -c "
            pip install pika > /dev/null 2>&1 && \
            export RABBITMQ_HOST=rabbitmq && \
            export RABBITMQ_USER=$RABBITMQ_USER && \
            export RABBITMQ_PASSWORD=$RABBITMQ_PASSWORD && \
            python3 /publish-file-message.py '$url' '$file_type' $print_job_id
        "
}

wait_for_services() {
    echo -e "${YELLOW}Waiting for services to be ready...${NC}"

    # Wait for RabbitMQ
    echo -n "Waiting for RabbitMQ"
    until curl -s -o /dev/null -w "%{http_code}" http://$RABBITMQ_USER:$RABBITMQ_PASSWORD@localhost:15672/api/overview | grep -q "200"; do
        echo -n "."
        sleep 2
    done
    echo -e "\n${GREEN}RabbitMQ is ready!${NC}"

    # Wait for File Server
    echo -n "Waiting for File Server"
    until curl -s -o /dev/null -w "%{http_code}" $FILE_SERVER_URL/health | grep -q "200"; do
        echo -n "."
        sleep 2
    done
    echo -e "\n${GREEN}File Server is ready!${NC}"

    # Wait for MinIO
    echo -n "Waiting for MinIO"
    until curl -s -o /dev/null -w "%{http_code}" http://localhost:9000/minio/health/live | grep -q "200"; do
        echo -n "."
        sleep 2
    done
    echo -e "\n${GREEN}MinIO is ready!${NC}"
}

test_local_file() {
    local file_path=$1
    local file_type=$2
    local print_job_id=$3
    local file_name=$(basename "$file_path")

    echo -e "\n${YELLOW}=== Testing Local File Processing ===${NC}"
    echo "File: $file_path"
    echo "File type: $file_type"
    [ -n "$print_job_id" ] && echo "Print Job ID: $print_job_id"

    # Start a temporary HTTP server in Docker
    echo "Starting temporary nginx server..."
    if ! docker run -d --rm \
        --name test-file-server \
        --network "$DOCKER_NETWORK" \
        -p $TEMP_PORT:80 \
        -v "$SCRIPT_DIR/TestFiles:/usr/share/nginx/html:ro" \
        nginx:alpine; then
        echo -e "${RED}Failed to start nginx container${NC}"
        return 1
    fi

    # Give nginx time to start
    sleep 3

    INTERNAL_URL="http://test-file-server:80/$file_name"

    echo -e "${YELLOW}Testing FileDownloaderService via RabbitMQ JobAccepted queue...${NC}"

    if ! send_rabbitmq_message "$INTERNAL_URL" "$file_type" "$print_job_id"; then
        echo -e "${RED}Failed to send message to RabbitMQ${NC}"
        docker stop test-file-server > /dev/null 2>&1
        return 1
    fi

    echo -e "${GREEN}✓ Local file test message sent successfully!${NC}"

    # Wait for file processing to complete before stopping nginx server
    echo "Waiting for file processing to complete (keeping nginx server running)..."
    sleep 3

    docker stop test-file-server > /dev/null 2>&1
}

test_external_url() {
    local url=$1
    local file_type=$2
    local print_job_id=$3

    echo -e "\n${YELLOW}=== Testing External URL Download ===${NC}"
    echo "URL: $url"
    echo "File type: $file_type"
    [ -n "$print_job_id" ] && echo "Print Job ID: $print_job_id"

    # Check if it's a Google Drive URL
    if [[ $url == *"drive.google.com"* ]]; then
        echo -e "${YELLOW}Google Drive URL detected - testing GoogleDriveService integration${NC}"
    fi

    if ! send_rabbitmq_message "$url" "$file_type" "$print_job_id"; then
        echo -e "${RED}Failed to send external URL message to RabbitMQ${NC}"
        return 1
    fi

    echo -e "${GREEN}✓ External URL test message sent successfully!${NC}"
}

test_google_drive_url() {
    local print_job_id=$1

    echo -e "\n${YELLOW}=== Testing Google Drive File Download ===${NC}"
    echo "URL: $GOOGLE_DRIVE_TEST_URL"
    [ -n "$print_job_id" ] && echo "Print Job ID: $print_job_id"

    if ! send_rabbitmq_message "$GOOGLE_DRIVE_TEST_URL" "gcode" "$print_job_id"; then
        echo -e "${RED}Failed to send Google Drive URL message to RabbitMQ${NC}"
        return 1
    fi

    echo -e "${GREEN}✓ Google Drive test message sent successfully!${NC}"
}

test_api_endpoints() {
    echo -e "\n${YELLOW}=== Testing API Endpoints ===${NC}"

    # Test health endpoint
    echo "Testing health endpoint..."
    HEALTH_RESPONSE=$(curl -s -w "%{http_code}" "$FILE_SERVER_URL/health")
    if echo "$HEALTH_RESPONSE" | grep -q "200"; then
        echo -e "${GREEN}✓ Health endpoint works${NC}"
    else
        echo -e "${RED}✗ Health endpoint failed${NC}"
        echo "Response: $HEALTH_RESPONSE"
    fi

    # Test gcode endpoints
    echo "Testing gcode file listing..."
    GCODE_LIST=$(curl -s "$FILE_SERVER_URL/api/gcode" || echo "FAILED")
    if echo "$GCODE_LIST" | grep -q "gcode-files"; then
        echo -e "${GREEN}✓ Gcode listing API works${NC}"
    else
        echo -e "${RED}✗ Gcode listing API failed${NC}"
        echo "Response: $GCODE_LIST"
    fi

    # Test individual gcode file if any exist
    FIRST_FILE=$(echo "$GCODE_LIST" | grep -o '"job_[0-9]*\.gcode"' | head -1 | tr -d '"' || echo "")
    if [ -n "$FIRST_FILE" ]; then
        # Extract job ID from filename like "job_12345.gcode"
        JOB_ID=$(echo "$FIRST_FILE" | grep -o '[0-9]\+' | head -1)
        if [ -n "$JOB_ID" ]; then
            echo "Testing gcode download for job ID $JOB_ID..."
            CONTENT_RESPONSE=$(curl -s -w "%{http_code}" "$FILE_SERVER_URL/api/gcode/$JOB_ID")
            if echo "$CONTENT_RESPONSE" | grep -q "200"; then
                echo -e "${GREEN}✓ Gcode download API works${NC}"
            else
                echo -e "${RED}✗ Gcode download API failed${NC}"
            fi

            echo "Testing gcode bytes endpoint for job ID $JOB_ID..."
            BYTES_RESPONSE=$(curl -s -w "%{http_code}" "$FILE_SERVER_URL/api/gcode/$JOB_ID/bytes")
            if echo "$BYTES_RESPONSE" | grep -q "200"; then
                echo -e "${GREEN}✓ Gcode bytes API works${NC}"
            else
                echo -e "${RED}✗ Gcode bytes API failed${NC}"
            fi
        fi
    else
        echo -e "${YELLOW} No gcode files found for detailed API tests${NC}"
    fi

    # Test image endpoints
    echo "Testing image file listing..."
    IMAGES_LIST=$(curl -s "$FILE_SERVER_URL/api/images" || echo "FAILED")
    if echo "$IMAGES_LIST" | grep -q "images"; then
        echo -e "${GREEN}✓ Images listing API works${NC}"
    else
        echo -e "${RED}✗ Images listing API failed${NC}"
        echo "Response: $IMAGES_LIST"
    fi

    # Test image upload with a sample file
    if [ -f "$SCRIPT_DIR/TestFiles/test-image.png" ]; then
        TEST_JOB_ID=99999
        echo "Testing image upload for job ID $TEST_JOB_ID..."
        UPLOAD_RESPONSE=$(curl -s -X POST \
            -F "file=@$SCRIPT_DIR/TestFiles/test-image.png" \
            "$FILE_SERVER_URL/api/images/$TEST_JOB_ID" || echo "FAILED")

        if echo "$UPLOAD_RESPONSE" | grep -q "success.*true"; then
            echo -e "${GREEN}✓ Image upload API works${NC}"

            # Test downloading the uploaded image
            echo "Testing image download for job ID $TEST_JOB_ID..."
            DOWNLOAD_STATUS=$(curl -s -w "%{http_code}" -o /dev/null "$FILE_SERVER_URL/api/images/$TEST_JOB_ID")
            if [ "$DOWNLOAD_STATUS" = "200" ]; then
                echo -e "${GREEN}✓ Image download API works${NC}"
            else
                echo -e "${RED}✗ Image download API failed (Status: $DOWNLOAD_STATUS)${NC}"
            fi
        else
            echo -e "${RED}✗ Image upload API failed${NC}"
            echo "Response: $UPLOAD_RESPONSE"
        fi
    fi

    # Test google drive
    GOOGLE_TEST_RESPONSE=$(curl -s -X POST \
-H "Content-Type: application/json" \
-d '{"fileId": "13Nzp3U4t6-HJMG-c5DEWA2pnT6Ios0Q4"}' \
"$FILE_SERVER_URL/api/test/google-drive-message" || echo "FAILED")

    if [ "$GOOGLE_TEST_RESPONSE" != "FAILED" ]; then
        echo -e "${GREEN}✓ Test Google Drive message endpoint works${NC}"
    else
        echo -e "${RED}✗ Test Google Drive message endpoint failed${NC}"
    fi
}

check_rabbitmq() {
    echo -e "\n${YELLOW}=== Checking RabbitMQ Queue Status ===${NC}"

    # Check JobAccepted queue status
    local queue_info=$(curl -s -u $RABBITMQ_USER:$RABBITMQ_PASSWORD http://localhost:15672/api/queues/%2F/JobAccepted 2>/dev/null)
    if [ -n "$queue_info" ]; then
        local messages=$(echo "$queue_info" | grep -o '"messages":[0-9]*' | grep -o '[0-9]*')
        local consumers=$(echo "$queue_info" | grep -o '"consumers":[0-9]*' | grep -o '[0-9]*')

        echo "JobAccepted queue status:"
        echo "  Messages: ${messages:-0}"
        echo "  Consumers: ${consumers:-0}"

        if [ -n "$messages" ] && [ "$messages" -gt 0 ]; then
            echo -e "${YELLOW} Warning: ${messages} unprocessed messages in JobAccepted queue${NC}"
        else
            echo -e "${GREEN}✓ JobAccepted queue processed all messages${NC}"
        fi
    else
        echo -e "${RED}✗ Could not get JobAccepted queue information${NC}"
    fi
}

check_minio() {
    echo -e "\n${YELLOW}=== Checking MinIO Storage Results ===${NC}"

    docker exec minio-server mc alias set "$MINIO_ALIAS" http://localhost:9000 "$MINIO_ROOT_USER" "$MINIO_ROOT_PASSWORD" > /dev/null 2>&1

    echo "Checking gcode-files bucket:"
    GCODE_FILES=$(docker exec minio-server mc ls "$MINIO_ALIAS/gcode-files" 2>/dev/null || echo "")
    if [ -n "$GCODE_FILES" ]; then
        echo "$GCODE_FILES"
        GCODE_COUNT=$(echo "$GCODE_FILES" | wc -l)
        echo -e "${GREEN}✓ Found $GCODE_COUNT GCode files successfully stored${NC}"
    else
        echo "  No gcode files found or bucket doesn't exist"
    fi

    echo -e "\nChecking images bucket:"
    IMAGE_FILES=$(docker exec minio-server mc ls "$MINIO_ALIAS/images" 2>/dev/null || echo "")
    if [ -n "$IMAGE_FILES" ]; then
        echo "$IMAGE_FILES"
        IMAGE_COUNT=$(echo "$IMAGE_FILES" | wc -l)
        echo -e "${GREEN}✓ Found $IMAGE_COUNT image files successfully stored${NC}"
    else
        echo "  No image files found or bucket doesn't exist"
    fi
}

show_sample_content() {
    echo -e "\n${YELLOW}=== Sample File Content ===${NC}"

    # Show sample gcode content via API
    GCODE_FILES=$(docker exec minio-server mc ls "$MINIO_ALIAS/gcode-files" 2>/dev/null | grep '\.gcode' | head -1 || echo "")
    if [ -n "$GCODE_FILES" ]; then
        FIRST_FILE=$(echo "$GCODE_FILES" | awk '{print $NF}')
        JOB_ID=$(echo "$FIRST_FILE" | grep -o '[0-9]\+' | head -1)

        if [ -n "$JOB_ID" ]; then
            echo "Sample gcode content from job $JOB_ID (first few lines):"
            curl -s "$FILE_SERVER_URL/api/gcode/$JOB_ID" | head -3 || echo "Could not retrieve content"
            echo "..."
        fi
    else
        echo "No gcode files available to show sample content"
    fi
}

main() {

    cd "$SCRIPT_DIR"

    check_env_vars
    check_services
    wait_for_services

    # Verify test files exist
    if [ ! -f "TestFiles/sample.gcode" ]; then
        echo -e "${RED}ERROR: Test file 'TestFiles/sample.gcode' not found!${NC}"
        echo "Please ensure test files are present in the TestFiles directory"
        exit 1
    fi

    case "${1:-all}" in
        "local")
            echo -e "${YELLOW}=== Testing Local Files Only ===${NC}"
            test_local_file "TestFiles/sample.gcode" "gcode" "10001"
            if [ -f "TestFiles/test-image.png" ]; then
                sleep 3
                test_local_file "TestFiles/test-image.png" "image" "10002"
            fi
            ;;
        "external")
            echo -e "${YELLOW}=== Testing External URL Only ===${NC}"
            test_external_url "$EXTERNAL_TEST_URL" "gcode" "20001"
            ;;
        "googledrive")
            echo -e "${YELLOW}=== Testing Google Drive URL Only ===${NC}"
            test_google_drive_url "25001"
            ;;
        "gcode")
            echo -e "${YELLOW}=== Testing GCode Processing Only ===${NC}"
            test_local_file "TestFiles/sample.gcode" "gcode" "30001"
            ;;
        "image")
            echo -e "${YELLOW}=== Testing Image Processing Only ===${NC}"
            if [ -f "TestFiles/test-image.png" ]; then
                test_local_file "TestFiles/test-image.png" "image" "30002"
            else
                echo -e "${RED}ERROR: Test file 'TestFiles/test-image.png' not found!${NC}"
                exit 1
            fi
            ;;
        "api")
            echo -e "${YELLOW}=== Testing API Endpoints Only ===${NC}"
            test_api_endpoints
            ;;
        "all")
            echo -e "${YELLOW}=== Running Complete Test Suite ===${NC}"
            test_local_file "TestFiles/sample.gcode" "gcode" "40001"
            sleep 3
            if [ -f "TestFiles/test-image.png" ]; then
                test_local_file "TestFiles/test-image.png" "image" "40002"
                sleep 3
            fi
            test_external_url "$EXTERNAL_TEST_URL" "gcode" "40003"
            sleep 3
            test_google_drive_url "40004"
            ;;
        "help"|"-h"|"--help")
            echo "Usage: $0 [OPTION]"
            echo ""
            echo "FileServer Integration Test Suite"
            echo "Tests RabbitMQ JobAccepted queue, API endpoints, and file processing"
            echo ""
            echo "Options:"
            echo "  all        Run all tests (local + external + Google Drive + API) [default]"
            echo "  local      Run only local file tests (gcode + image)"
            echo "  external   Run only external URL test"
            echo "  googledrive Run only Google Drive URL test"
            echo "  gcode      Run only local gcode file test"
            echo "  image      Run only local image file test"
            echo "  api        Run only API endpoint tests"
            echo "  help       Show this help message"
            echo ""
            echo "Key Features Tested:"
            echo "  - RabbitMQ JobAccepted queue message processing"
            echo "  - FileDownloaderService with Google Drive integration"
            echo "  - MinioService file storage operations"
            echo "  - GCode and Image API endpoints"
            echo "  - Test endpoints for development"
            echo ""
            echo "Examples:"
            echo "  $0             # Run all tests"
            echo "  $0 local       # Test local files only"
            echo "  $0 api         # Test API endpoints only"
            echo "  $0 googledrive # Test Google Drive integration"
            exit 0
            ;;
        *)
            echo "Invalid option: $1"
            echo "Usage: $0 [all|local|external|googledrive|gcode|image|api|help]"
            echo "Run '$0 help' for more information"
            exit 1
            ;;
    esac

    # Wait for file processing
    echo -e "\n${YELLOW}Waiting for file processing to complete...${NC}"

    # Give more time for external downloads
    if [[ "$1" == "external" || "$1" == "googledrive" || "$1" == "all" ]]; then
        sleep $PROCESSING_TIMEOUT_LONG
    else
        sleep $PROCESSING_TIMEOUT_SHORT
    fi

    # Run verification tests
    if [[ "$1" != "api" ]]; then
        test_api_endpoints
    fi

    check_rabbitmq
    check_minio
    show_sample_content

    echo -e "\n${GREEN}=== Integration Test Suite Completed! ===${NC}"
    echo ""
    echo "Monitoring Links:"
    echo "  - FileServer Health: $FILE_SERVER_URL/health"
    echo "  - RabbitMQ Management: http://localhost:15672"
    echo "  - MinIO Console: http://localhost:9001"
    echo ""
    echo "Test completed successfully! Check the output above for any failed tests."
}

# Run main function
main "$@"
