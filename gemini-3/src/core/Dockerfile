# Start with the official MySQL image
FROM mysql:8.0

# Copy the initialization scripts into the container
COPY docker-entrypoint-initdb.d/*.sql /docker-entrypoint-initdb.d/

EXPOSE 3307
