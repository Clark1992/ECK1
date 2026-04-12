# Stage 1: Build
FROM node:22-alpine AS build
WORKDIR /app
COPY src/ECK1.FE/package.json src/ECK1.FE/package-lock.json* ./
RUN npm ci
COPY src/ECK1.FE/ .
RUN npm run build

# Stage 2: Serve with Nginx
FROM nginx:1.27-alpine

# Remove default config
RUN rm /etc/nginx/conf.d/default.conf

# Copy nginx config template
COPY src/ECK1.FE/nginx.conf.template /etc/nginx/conf.d/default.conf.template

# Copy entrypoint
COPY src/ECK1.FE/docker-entrypoint.sh /docker-entrypoint.sh
RUN chmod +x /docker-entrypoint.sh

# Copy built assets
COPY --from=build /app/dist /usr/share/nginx/html

# Run as non-root
RUN chown -R nginx:nginx /usr/share/nginx/html && \
    chown -R nginx:nginx /var/cache/nginx && \
    chown -R nginx:nginx /var/log/nginx && \
    chown -R nginx:nginx /etc/nginx/conf.d && \
    touch /var/run/nginx.pid && \
    chown nginx:nginx /var/run/nginx.pid

USER nginx

EXPOSE 80

ENTRYPOINT ["/docker-entrypoint.sh"]
