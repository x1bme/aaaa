# Build stage
FROM node:18-alpine AS build
WORKDIR /app

# Install pnpm
RUN npm install -g pnpm
RUN npm install -g @quasar/cli

# Copy source code
COPY . .

# Install dependencies
RUN pnpm install

# Build the app
RUN quasar build

# Production stage
FROM nginx:alpine
WORKDIR /usr/share/nginx/html

# Copy built app from build stage
COPY --from=build /app/dist/spa .

# Copy nginx configuration
COPY ./nginx.conf /etc/nginx/conf.d/default.conf

# Expose port
EXPOSE 80 443

# Start nginx
CMD ["nginx", "-g", "daemon off;"]
