# Postgres

Creates a PostgreSQL database for handling users, printers, print jobs, etc. The source of truth for the state of the
OpenFarm system.

## Dockerfile Explanation

The Dockerfile is conveniently very simple. So simple in fact, it is shown in its entirety below.

```dockerfile
FROM postgres:17

COPY init-db/ /docker-entrypoint-initdb.d/

HEALTHCHECK --interval=5s --timeout=5s --retries=5 \
    CMD pg_isready -U $POSTGRES_USER -d $POSTGRES_DB || exit 1

EXPOSE 5432
```

This image is first based from the official PostgreSQL image, specifically PostgreSQL version 17 (the most recent,
stable version at time of creation).

The contents from the init-db/ directory are then copied into the /docker-entrypoint-initdb.d/ directory inside the
image.

- The files copied into the /docker-entrypoint-initdb.d/ directory will be automatically executed by the PostgreSQL
  entrypoint script.

A check to see if the container is healthy is then run.

A command is then run that checks if the PostgreSQL server is ready to accept connections for the specified user (
\$POSTGRES_USER) and database (\$POSTGRES_DB). If the check fails, the command exits with an error code (1), signaling
failure.

- \$POSTGRES_USER and $POSTGRES_DB are environment variables from your .env file. If you do not have a .env file or have
  questions about the .env file, check the README.md file located in the root directory from the dev branch for more
  information.

Finally port 5432 is exposed from the Docker container to listen on at run time.

## Init Scripts

These scripts run only once at initialization.

**IMPORTANT NOTE**: All scripts within init-db/ whether sql, shell, etc. Will be run in alphabetical order. Hence, the
naming convention of ordering the scripts numerically. 01-init.sql should be run before 02-populate.sql. If you add
anymore scripts ensure to follow this convention to correctly order the scripts when they are run.

- **01-init.sql**: Creates the relations and tables for the database.

- **02-populate.sql**: Populates the database with important information for the OpenFarm system.

