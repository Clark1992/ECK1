#!/bin/sh
set -e

echo "Starting MongoDB migrations..."
echo "Using: $MONGO_URL / $MONGO_DB"

cd /workdir

npm install -g migrate-mongo

migrate-mongo up

echo "MongoDB migrations completed."
