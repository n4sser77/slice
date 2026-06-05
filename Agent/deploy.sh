#!/bin/bash

# Exit immediately if any command fails
set -e

echo "🔄 Pulling latest changes from Git..."
git pull

echo "🛑 Stopping the slice-agent service..."
systemctl --user stop slice-agent.service

echo "📦 Publishing dotnet application..."
dotnet publish -c Release -r linux-arm64 --self-contained false -p:PublishAot=false

echo "🚀 Starting the slice-agent service..."
systemctl --user start slice-agent.service

echo "✅ Deployment completed successfully!"
