# Printer Management Service (PMS)

The Printer Management Service is a .NET 9.0 Worker Service that orchestrates 3D printing operations in the OpenFarm
system. It serves as the central coordinator between print job requests, 3D printer hardware, and system state
management.

## Overview

PMS acts as the brain of the 3D printing infrastructure, managing multiple printer instances, processing print jobs from
a queue, and maintaining real-time synchronization between the database, message queue, and physical printers.

## Key Features

- **Multi-Printer Management**: Simultaneously manages multiple 3D printers through OctoPrint instances
- **Job Queue Processing**: Processes print jobs from RabbitMQ with automatic retry and error handling
- **Real-Time Monitoring**: Continuously monitors printer states and job progress
- **Automatic Recovery**: Handles printer disconnections and attempts automatic reconnection
- **Database Synchronization**: Maintains consistent state between physical printers and database records
- **Copy Management**: Supports printing multiple copies of the same job across available printers
- **Docker Support**: Fully containerized for easy deployment and scaling

## Components

### PMSWorker

The main background service that runs the printing coordination loop:

- Processes enqueued jobs from RabbitMQ
- Manages printer registry and connections
- Monitors print progress and completion
- Updates database state in real-time

### RegisteredInstance

Represents a registered 3D printer with its connection state, capabilities, and current job information.

### Helper Classes

- **DbHelpers**: Database update operations for jobs, prints, and printers
- **RmqHelpers**: RabbitMQ message publishing for system notifications

## Configuration

TODO

Additional configuration may be required for:

- RabbitMQ connection settings
- OctoPrint instance configurations
- Logging levels and destinations

## Installation & Setup

## Operation Flow

1. **Startup**: Service registers all configured printers from database
2. **Job Reception**: Listens for paid job notifications via RabbitMQ
3. **Job Processing**: Creates print copies and queues them for processing
4. **Printer Assignment**: Assigns jobs to available printers based on state
5. **Print Execution**: Downloads GCode and initiates printing via OctoPrint
6. **Progress Monitoring**: Continuously monitors print progress and printer states
7. **Completion Handling**: Processes completed prints and updates system state

## Monitoring & Logging

The service provides extensive logging for:

- Printer connection status changes
- Job processing events
- Print progress updates
- Error conditions and recovery attempts
- System health indicators

Logs are structured and include:

- Timestamp and log level
- Component context (printer ID, job ID)
- Detailed operation descriptions
- Error messages and stack traces

## Error Handling

The service implements robust error handling:

- **Connection Failures**: Automatic printer reconnection attempts
- **Job Failures**: Error logging with job state preservation
- **API Errors**: Retry logic with exponential backoff
- **System Failures**: Graceful shutdown with state preservation

## Performance Considerations

- **Concurrent Operations**: Uses thread-safe collections for printer registry
- **Polling Intervals**: Configurable polling frequency (default: 10 seconds)
- **Resource Management**: Proper disposal of HTTP clients and connections
- **Memory Efficiency**: Streaming G-code transfers to minimize memory usage

## API Integration

### RabbitMQ Integration

- Subscribes to `JobPaid` queue for new job notifications
- Publishes print status updates to notification queues

### Database Integration

- Manages printer, job, and print records
- Maintains transactional consistency for state updates

### OctoPrint Integration

- Communicates with multiple OctoPrint instances
- Handles file uploads, print control, and status monitoring

## Troubleshooting

### Common Issues

**Printer Connection Failures**

- Check printer IP addresses and API keys
- Verify network connectivity to OctoPrint instances
- Review printer-specific logs for hardware issues

**Job Processing Delays**

- Monitor database connection health
- Check RabbitMQ queue depths and processing rates
- Verify file server availability and response times

**Memory Issues**

- Monitor GCode file sizes and transfer patterns
- Check for connection leaks in HTTP clients
- Review concurrent job processing limits

## License

[TODO]

## Support

For issues and questions:

- Check the troubleshooting section above
- Review service logs for error details
- Lament your poor luck by shaking your fist at the sun (or moon if it is nighttime)