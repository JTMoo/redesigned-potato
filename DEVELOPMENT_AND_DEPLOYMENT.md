# Development Setup & Deployment Guide

## Repository Preparation

### Directory Structure

```
expense-tracker-deals/
├── docker-compose.yml                 # Local development orchestration
├── .dockerignore
├── .gitignore
├── LICENSE
├── README.md
│
├── services/
│   ├── api-gateway/
│   │   ├── Dockerfile
│   │   ├── src/
│   │   ├── .github/workflows/
│   │   │   └── build-and-test.yml
│   │   └── api-gateway.csproj
│   │
│   ├── user-service/
│   │   ├── Dockerfile
│   │   ├── src/
│   │   ├── .github/workflows/
│   │   │   └── build-and-test.yml
│   │   └── user-service.csproj
│   │
│   ├── receipt-service/
│   │   ├── Dockerfile
│   │   ├── src/
│   │   ├── .github/workflows/
│   │   └── receipt-service.csproj
│   │
│   ├── deal-service/
│   │   ├── Dockerfile
│   │   ├── src/
│   │   ├── .github/workflows/
│   │   └── deal-service.csproj
│   │
│   ├── matching-service/
│   │   ├── Dockerfile
│   │   ├── src/
│   │   ├── .github/workflows/
│   │   └── matching-service.csproj
│   │
│   ├── aggregation-service/
│   │   ├── Dockerfile
│   │   ├── src/
│   │   ├── .github/workflows/
│   │   └── aggregation-service.csproj
│   │
│   └── notification-service/
│       ├── Dockerfile
│       ├── src/
│       ├── .github/workflows/
│       └── notification-service.csproj
│
├── frontend/
│   ├── Dockerfile
│   ├── package.json
│   ├── src/
│   ├── public/
│   ├── .github/workflows/
│   │   └── build-and-test.yml
│   └── .dockerignore
│
├── shared/
│   ├── contracts/                    # Shared event/API contracts (NuGet package)
│   │   └── EventContracts.csproj
│   └── utilities/                    # Shared code (logging, helpers)
│       └── Utilities.csproj
│
├── .github/
│   └── workflows/
│       ├── integration-tests.yml     # Full stack integration tests
│       └── docs-build.yml            # API docs generation
│
├── infrastructure/
│   ├── docker-compose.yml            # Production docker-compose template
│   ├── docker-compose.dev.yml        # Dev overrides
│   ├── docker-compose.prod.yml       # Prod overrides
│   ├── k8s/                          # Kubernetes manifests (future)
│   └── monitoring/
│       ├── seq-docker-compose.yml    # Logging setup
│       └── prometheus.yml            # Metrics (future)
│
└── docs/
    ├── ARCHITECTURE.md               # Architecture decisions
    ├── API.md                        # API specifications
    ├── EVENTS.md                     # Event schema and contracts
    └── DEVELOPMENT.md                # How to develop locally
```

---

## Local Development Requirements

### Required Tools

```
Docker Desktop 4.20+              # Containers and docker-compose
.NET 10 SDK                       # C# 14 services
Node.js 20+ LTS                   # React frontend
Git                               # Version control
Visual Studio Code or JetBrains   # IDE
```

### Install & Verify

```bash
# Docker
docker --version
docker-compose --version

# .NET 10 (verify you have .NET 10, not 8)
dotnet --version  # Should show 10.0.x

# Node
node --version
npm --version

# Git
git --version
```

---

## Getting Started Locally

### 1. Clone & Initialize

```bash
git clone <repo-url> expense-tracker-deals
cd expense-tracker-deals

# Install git hooks (optional, for auto-formatting)
git config core.hooksPath .githooks
```

### 2. Environment Configuration

Create `.env` files for local development:

```bash
# Root directory - .env.local
RABBITMQ_DEFAULT_USER=guest
RABBITMQ_DEFAULT_PASS=guest
POSTGRES_PASSWORD=localpassword
ASPNETCORE_ENVIRONMENT=Development
NODE_ENV=development
```

Create service-specific env files:

```bash
# services/user-service/.env.local
DATABASE_URL=postgres://postgres:localpassword@postgres-users:5432/users_db
RABBITMQ_URL=amqp://guest:guest@rabbitmq:5672
JWT_SECRET=dev-secret-key-change-in-production
OAUTH_PROVIDER_ID=google  # or your provider
OAUTH_CLIENT_ID=your-dev-client-id
OAUTH_CLIENT_SECRET=your-dev-secret
```

Repeat for other services (receipt-service, deal-service, etc.).

### 3. Start Full Stack

```bash
# From repo root
docker-compose up -d

# Watch logs
docker-compose logs -f

# Verify services are healthy
docker-compose ps

# Check a specific service
docker-compose logs user-service
```

### 4. Migrations Run Automatically

Migrations are applied automatically on service startup via `db.Database.MigrateAsync()` in Program.cs. Check logs to confirm:

```bash
docker-compose logs user-service | grep -i migration
```

If you need to manually run migrations:

```bash
# For a specific service
docker-compose exec user-service dotnet ef database update

# Or before starting service
docker-compose exec user-service dotnet ef migrations list
```

See DATA_ACCESS_GUIDE.md for detailed migration management.

### 5. Verify Everything Works

```bash
# API Gateway health check
curl http://localhost:5000/health

# RabbitMQ management UI
open http://localhost:15672
# Login: guest / guest

# React frontend
open http://localhost:3000
```

### 6. Develop

```bash
# Backend: Changes auto-reload via dotnet watch (if configured in Dockerfile)
# Frontend: Changes auto-reload via React hot reload

# Run tests for a service
docker-compose exec user-service dotnet test

# Stop everything
docker-compose down
```

### Data Access

All services use EF Core DbContext directly (no repositories). Code-first migrations only.

- **For schema changes**: See DATA_ACCESS_GUIDE.md - generate migrations, review, apply
- **For entity definitions**: Domain/ folder in each service
- **For queries**: Inject DbContext directly into handlers/services

---

## Deployment to External Server

### Option 1: Docker Compose on VPS (Simplest)

**Good for:** Learning, small projects, single-server deployments

**Requirements:**
- VPS with 4GB RAM, 2 CPU minimum
- Docker and docker-compose installed
- Domain name pointing to server
- SSL certificate (Let's Encrypt)

**Steps:**

1. **Create production compose file** (`infrastructure/docker-compose.prod.yml`):

```yaml
version: '3.9'

services:
  # Reverse proxy with SSL
  nginx:
    image: nginx:alpine
    ports:
      - "80:80"
      - "443:443"
    volumes:
      - ./nginx.conf:/etc/nginx/nginx.conf:ro
      - /etc/letsencrypt:/etc/letsencrypt:ro
    depends_on:
      - api-gateway

  rabbitmq:
    image: rabbitmq:3.12-management
    volumes:
      - rabbitmq-data:/var/lib/rabbitmq
    environment:
      RABBITMQ_DEFAULT_USER: ${RABBITMQ_USER}
      RABBITMQ_DEFAULT_PASS: ${RABBITMQ_PASS}

  postgres-users:
    image: postgres:15
    environment:
      POSTGRES_PASSWORD: ${DB_PASSWORD}
    volumes:
      - postgres-users:/var/lib/postgresql/data
    restart: unless-stopped

  # ... other databases ...

  api-gateway:
    image: your-registry/api-gateway:latest
    restart: unless-stopped
    environment:
      ASPNETCORE_ENVIRONMENT: Production
      DATABASE_URL: ${DB_URL}

  user-service:
    image: your-registry/user-service:latest
    restart: unless-stopped
    environment:
      ASPNETCORE_ENVIRONMENT: Production
      DATABASE_URL: ${POSTGRES_USERS_URL}

  # ... other services ...

  react-app:
    image: your-registry/react-app:latest
    restart: unless-stopped

volumes:
  rabbitmq-data:
  postgres-users:
  # ... other volumes ...
```

2. **Set up on server:**

```bash
# SSH into server
ssh user@your-server.com

# Clone repo
git clone <repo-url>
cd expense-tracker-deals

# Create .env with secrets
cat > .env.prod << EOF
RABBITMQ_USER=secure-user
RABBITMQ_PASS=$(openssl rand -base64 32)
DB_PASSWORD=$(openssl rand -base64 32)
ASPNETCORE_ENVIRONMENT=Production
EOF

# Start stack
docker-compose -f docker-compose.prod.yml up -d

# Set up SSL with certbot
sudo certbot certonly --standalone -d your-domain.com
```

3. **Nginx config** (reverse proxy + SSL):

```nginx
upstream api_gateway {
    server api-gateway:5000;
}

server {
    listen 443 ssl http2;
    server_name your-domain.com;

    ssl_certificate /etc/letsencrypt/live/your-domain.com/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/your-domain.com/privkey.pem;

    location /api {
        proxy_pass http://api_gateway;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }

    location / {
        proxy_pass http://react-app:3000;
    }
}

server {
    listen 80;
    server_name your-domain.com;
    return 301 https://$server_name$request_uri;
}
```

---

### Option 2: Kubernetes Cluster (Scalable)

**Good for:** Production, scaling, multiple replicas, advanced features

**Requirements:**
- Kubernetes cluster (EKS, GKE, DigitalOcean, or self-hosted)
- kubectl CLI
- Docker image registry (Docker Hub, ECR, etc.)
- Helm (optional, for templating)

**Basic setup:**

```yaml
# k8s/namespace.yaml
apiVersion: v1
kind: Namespace
metadata:
  name: expense-tracker

---
# k8s/configmap.yaml
apiVersion: v1
kind: ConfigMap
metadata:
  namespace: expense-tracker
  name: app-config
data:
  ASPNETCORE_ENVIRONMENT: Production
  RABBITMQ_HOST: rabbitmq-service

---
# k8s/secret.yaml (create manually or with sealed-secrets)
apiVersion: v1
kind: Secret
metadata:
  namespace: expense-tracker
  name: db-credentials
type: Opaque
data:
  POSTGRES_PASSWORD: <base64-encoded>
  RABBITMQ_PASS: <base64-encoded>

---
# k8s/user-service-deployment.yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  namespace: expense-tracker
  name: user-service
spec:
  replicas: 3
  selector:
    matchLabels:
      app: user-service
  template:
    metadata:
      labels:
        app: user-service
    spec:
      containers:
      - name: user-service
        image: your-registry/user-service:latest
        ports:
        - containerPort: 8080
        env:
        - name: ASPNETCORE_ENVIRONMENT
          valueFrom:
            configMapKeyRef:
              name: app-config
              key: ASPNETCORE_ENVIRONMENT
        - name: DATABASE_URL
          valueFrom:
            secretKeyRef:
              name: db-credentials
              key: DATABASE_URL
        livenessProbe:
          httpGet:
            path: /health
            port: 8080
          initialDelaySeconds: 10
          periodSeconds: 10
        readinessProbe:
          httpGet:
            path: /ready
            port: 8080
          initialDelaySeconds: 5
          periodSeconds: 5

---
# k8s/user-service-service.yaml
apiVersion: v1
kind: Service
metadata:
  namespace: expense-tracker
  name: user-service
spec:
  selector:
    app: user-service
  ports:
  - port: 8080
    targetPort: 8080
  type: ClusterIP
```

Deploy:

```bash
kubectl apply -f k8s/namespace.yaml
kubectl apply -f k8s/configmap.yaml
kubectl apply -f k8s/secret.yaml
kubectl apply -f k8s/user-service-deployment.yaml
kubectl apply -f k8s/user-service-service.yaml

# Verify
kubectl -n expense-tracker get pods
kubectl -n expense-tracker logs deployment/user-service
```

---

### Option 3: Managed Services (Easiest, Not Learning-Focused)

Use cloud platforms that handle orchestration:

- **Google Cloud Run**: Containerized services, pay-per-invocation
- **AWS ECS**: Managed container orchestration
- **DigitalOcean App Platform**: Simple deployment UI
- **Heroku**: Git-based deployment (simplest, higher cost)

**DigitalOcean App Platform example:**

1. Push images to Docker Hub
2. Create app.yaml:

```yaml
name: expense-tracker-deals
services:
- name: api-gateway
  github:
    repo: your-username/expense-tracker-deals
    branch: main
  build_command: docker build -t api-gateway services/api-gateway
  http_port: 5000
  
- name: user-service
  github:
    repo: your-username/expense-tracker-deals
    branch: main
  build_command: docker build -t user-service services/user-service
```

3. Deploy via DigitalOcean dashboard

---

## CI/CD Pipeline Setup

### GitHub Actions Example

`.github/workflows/build-and-deploy.yml`:

```yaml
name: Build and Deploy

on:
  push:
    branches: [main]
  pull_request:
    branches: [main]

jobs:
  test:
    runs-on: ubuntu-latest
    services:
      postgres:
        image: postgres:15
        env:
          POSTGRES_PASSWORD: test
      rabbitmq:
        image: rabbitmq:3.12

    steps:
    - uses: actions/checkout@v3
    
    - uses: actions/setup-dotnet@v3
      with:
        dotnet-version: '10.0'
    
    - uses: actions/setup-node@v3
      with:
        node-version: '20'
    
    # Test all services
    - name: Run backend tests
      run: |
        for service in services/*/; do
          cd "$service"
          dotnet test
          cd ../..
        done
    
    - name: Run frontend tests
      run: |
        cd frontend
        npm ci
        npm test -- --coverage
    
    # Build Docker images
    - name: Build Docker images
      run: docker-compose build
    
    # Push to registry if main branch
    - name: Push to registry
      if: github.ref == 'refs/heads/main'
      run: |
        echo "${{ secrets.DOCKER_REGISTRY_PASSWORD }}" | \
          docker login -u "${{ secrets.DOCKER_REGISTRY_USER }}" --password-stdin
        docker-compose push

  deploy:
    needs: test
    if: github.ref == 'refs/heads/main'
    runs-on: ubuntu-latest
    
    steps:
    - uses: actions/checkout@v3
    
    - name: Deploy to VPS
      run: |
        mkdir -p ~/.ssh
        echo "${{ secrets.DEPLOY_KEY }}" > ~/.ssh/deploy_key
        chmod 600 ~/.ssh/deploy_key
        ssh -i ~/.ssh/deploy_key user@your-server.com \
          "cd expense-tracker-deals && \
           git pull origin main && \
           docker-compose -f docker-compose.prod.yml pull && \
           docker-compose -f docker-compose.prod.yml up -d"
```

---

## Pre-Launch Checklist

### Code Quality
- [ ] All services have unit tests (>80% coverage)
- [ ] Integration tests pass locally
- [ ] No hardcoded secrets in code
- [ ] API documentation generated and up-to-date

### Infrastructure
- [ ] docker-compose.yml tested locally
- [ ] docker-compose.prod.yml created with overrides
- [ ] Environment variables documented in `.env.example`
- [ ] Database migrations automated
- [ ] Backups configured (if persistent data)

### Security
- [ ] JWT secrets generated (not "dev-secret")
- [ ] Database passwords are strong
- [ ] RabbitMQ credentials changed from defaults
- [ ] SSL certificates obtained
- [ ] CORS configured (only allow frontend domain)
- [ ] Rate limiting configured
- [ ] Input validation on all endpoints

### Monitoring
- [ ] Seq/ELK logging configured
- [ ] Health check endpoints on all services
- [ ] Alerts set up (disk space, memory, service down)
- [ ] Log retention policy set

### Documentation
- [ ] README with setup instructions
- [ ] API documentation (Swagger/OpenAPI)
- [ ] Event schema documented
- [ ] Deployment runbook created
- [ ] Troubleshooting guide written

---

## Quick Reference

### Local Development
```bash
docker-compose up -d
docker-compose logs -f
docker-compose down
```

### Deploy to VPS
```bash
ssh user@server
cd expense-tracker-deals
git pull
docker-compose -f docker-compose.prod.yml up -d
```

### View Logs
```bash
docker-compose logs -f service-name
# or on server
docker-compose -f docker-compose.prod.yml logs -f service-name
```

### Scale a Service
```bash
docker-compose up -d --scale user-service=3
```

### Update a Service
```bash
docker-compose build user-service
docker-compose up -d user-service
```
