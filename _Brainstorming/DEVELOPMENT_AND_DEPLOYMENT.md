# Development Setup & Deployment Guide

## Repository Structure

```
expense-tracker/
├── docker-compose.yml                 # Local development orchestration
├── docker-compose.dev.yml             # Hot-reload volume overrides
├── .env.example                       # All required env var names (no secrets)
├── .gitignore
├── README.md
│
├── services/
│   ├── api-gateway/                   # YARP + Google OAuth + JWT
│   │   ├── Dockerfile
│   │   └── src/
│   │
│   ├── user-service/
│   │   ├── Dockerfile
│   │   └── src/
│   │
│   ├── receipt-service/
│   │   ├── Dockerfile
│   │   └── src/
│   │
│   ├── deal-service/
│   │   ├── Dockerfile
│   │   └── src/
│   │
│   ├── matching-service/
│   │   ├── Dockerfile
│   │   └── src/
│   │
│   ├── aggregation-service/
│   │   ├── Dockerfile
│   │   └── src/
│   │
│   └── notification-service/
│       ├── Dockerfile
│       └── src/
│
├── frontend/
│   └── react-app/                     # Vite + React + Google OAuth
│       ├── Dockerfile
│       ├── package.json
│       ├── src/
│       └── public/
│
├── shared/
│   ├── contracts/                     # Shared event records (C# class library)
│   │   └── EventContracts.csproj
│   └── utilities/                     # Shared helpers
│       └── Utilities.csproj
│
├── .github/
│   └── workflows/
│       ├── build-and-test.yml         # Builds and tests all services
│       └── integration-tests.yml      # Full stack integration tests
│
└── infrastructure/
    ├── docker-compose.prod.yml        # Production overrides
    ├── k8s/                           # Kubernetes manifests (future)
    └── monitoring/
        └── seq-docker-compose.yml     # Seq logging (included in main compose for dev)
```

---

## Local Development Requirements

### Required Tools

```
Docker Desktop 4.20+    # Containers and docker-compose
.NET 10 SDK             # C# 14 services
Node.js 20+ LTS         # React frontend
Git
Visual Studio Code or JetBrains Rider
```

### Verify Installation

```bash
dotnet --version    # Must show 10.x.x
docker --version
node --version      # Must show 20.x.x or higher
npm --version
git --version
```

---

## Getting Started Locally

### 1. Clone & Initialize

```bash
git clone <repo-url> expense-tracker
cd expense-tracker
```

### 2. Environment Configuration

Create `.env` for local development (never commit this file):

```bash
# Root .env (copy from .env.example)
cp .env.example .env
```

The `.env.example` documents all required variables:

```bash
# RabbitMQ
RABBITMQ_DEFAULT_USER=guest
RABBITMQ_DEFAULT_PASS=guest

# PostgreSQL
POSTGRES_PASSWORD=localpassword

# JWT
JWT_SECRET=dev-secret-key-change-in-production-minimum-32-chars

# Google OAuth
GOOGLE_CLIENT_ID=your-google-client-id
GOOGLE_CLIENT_SECRET=your-google-client-secret
GOOGLE_REDIRECT_URI=http://localhost:5000/auth/callback

# App
ASPNETCORE_ENVIRONMENT=Development
NODE_ENV=development
```

### 3. Start Full Stack

```bash
docker-compose up -d

# Watch logs
docker-compose logs -f

# Verify all services are healthy
docker-compose ps
```

### 4. Migrations Run Automatically

EF migrations run on startup via `dbContext.Database.MigrateAsync()` in each service's `Program.cs`.

```bash
# Confirm migrations ran
docker-compose logs user-service | grep -i migration
```

### 5. Verify Everything Works

```bash
# API Gateway health
curl http://localhost:5000/health

# Individual service health (via gateway routing)
curl http://localhost:5000/api/users/health
curl http://localhost:5000/api/receipts/health
curl http://localhost:5000/api/deals/health

# RabbitMQ management UI
open http://localhost:15672        # guest / guest

# Seq log viewer
open http://localhost:5341

# React frontend
open http://localhost:3000
```

---

## Port Assignments (Local Dev)

| Service | Port |
|---|---|
| React frontend | 3000 |
| API Gateway | 5000 |
| User Service | 5001 (internal only) |
| Receipt Service | 5002 (internal only) |
| Deal Service | 5003 (internal only) |
| Matching Service | 5004 (internal only) |
| Aggregation Service | 5005 (internal only) |
| Notification Service | 5006 (internal only) |
| RabbitMQ AMQP | 5672 (internal) |
| RabbitMQ Management | 15672 |
| Seq | 5341 |

Only the Gateway (5000) and frontend (3000) are exposed to the host. All backend services communicate internally via Docker network.

---

## docker-compose.yml (Local Dev Skeleton)

```yaml
version: '3.9'

services:
  # Message broker
  rabbitmq:
    image: rabbitmq:3.12-management
    ports:
      - "5672:5672"
      - "15672:15672"
    environment:
      RABBITMQ_DEFAULT_USER: ${RABBITMQ_DEFAULT_USER}
      RABBITMQ_DEFAULT_PASS: ${RABBITMQ_DEFAULT_PASS}
    healthcheck:
      test: rabbitmq-diagnostics -q ping
      interval: 10s
      timeout: 5s
      retries: 5

  # Logging
  seq:
    image: datalust/seq:latest
    ports:
      - "5341:80"
    environment:
      ACCEPT_EULA: Y

  # Databases
  postgres-users:
    image: postgres:15
    environment:
      POSTGRES_DB: users_db
      POSTGRES_PASSWORD: ${POSTGRES_PASSWORD}
    volumes:
      - users-data:/var/lib/postgresql/data

  postgres-receipts:
    image: postgres:15
    environment:
      POSTGRES_DB: receipts_db
      POSTGRES_PASSWORD: ${POSTGRES_PASSWORD}
    volumes:
      - receipts-data:/var/lib/postgresql/data

  postgres-deals:
    image: postgres:15
    environment:
      POSTGRES_DB: deals_db
      POSTGRES_PASSWORD: ${POSTGRES_PASSWORD}
    volumes:
      - deals-data:/var/lib/postgresql/data

  postgres-matching:
    image: postgres:15
    environment:
      POSTGRES_DB: matching_db
      POSTGRES_PASSWORD: ${POSTGRES_PASSWORD}
    volumes:
      - matching-data:/var/lib/postgresql/data

  postgres-aggregation:
    image: postgres:15
    environment:
      POSTGRES_DB: aggregation_db
      POSTGRES_PASSWORD: ${POSTGRES_PASSWORD}
    volumes:
      - aggregation-data:/var/lib/postgresql/data

  postgres-notifications:
    image: postgres:15
    environment:
      POSTGRES_DB: notifications_db
      POSTGRES_PASSWORD: ${POSTGRES_PASSWORD}
    volumes:
      - notifications-data:/var/lib/postgresql/data

  # Services
  api-gateway:
    build: ./services/api-gateway
    ports:
      - "5000:8080"
    environment:
      ASPNETCORE_ENVIRONMENT: Development
      RABBITMQ_URL: amqp://${RABBITMQ_DEFAULT_USER}:${RABBITMQ_DEFAULT_PASS}@rabbitmq:5672
      JWT_SECRET: ${JWT_SECRET}
      GOOGLE_CLIENT_ID: ${GOOGLE_CLIENT_ID}
      GOOGLE_CLIENT_SECRET: ${GOOGLE_CLIENT_SECRET}
      GOOGLE_REDIRECT_URI: ${GOOGLE_REDIRECT_URI}
      SEQ_URL: http://seq:80
    depends_on:
      rabbitmq:
        condition: service_healthy

  user-service:
    build: ./services/user-service
    environment:
      ASPNETCORE_ENVIRONMENT: Development
      DATABASE_URL: Host=postgres-users;Port=5432;Database=users_db;Username=postgres;Password=${POSTGRES_PASSWORD};
      RABBITMQ_URL: amqp://${RABBITMQ_DEFAULT_USER}:${RABBITMQ_DEFAULT_PASS}@rabbitmq:5672
      SEQ_URL: http://seq:80
    depends_on:
      rabbitmq:
        condition: service_healthy
      postgres-users:
        condition: service_started

  receipt-service:
    build: ./services/receipt-service
    environment:
      ASPNETCORE_ENVIRONMENT: Development
      DATABASE_URL: Host=postgres-receipts;Port=5432;Database=receipts_db;Username=postgres;Password=${POSTGRES_PASSWORD};
      RABBITMQ_URL: amqp://${RABBITMQ_DEFAULT_USER}:${RABBITMQ_DEFAULT_PASS}@rabbitmq:5672
      SEQ_URL: http://seq:80
    depends_on:
      rabbitmq:
        condition: service_healthy
      postgres-receipts:
        condition: service_started

  deal-service:
    build: ./services/deal-service
    environment:
      ASPNETCORE_ENVIRONMENT: Development
      DATABASE_URL: Host=postgres-deals;Port=5432;Database=deals_db;Username=postgres;Password=${POSTGRES_PASSWORD};
      RABBITMQ_URL: amqp://${RABBITMQ_DEFAULT_USER}:${RABBITMQ_DEFAULT_PASS}@rabbitmq:5672
      SEQ_URL: http://seq:80
    depends_on:
      rabbitmq:
        condition: service_healthy
      postgres-deals:
        condition: service_started

  matching-service:
    build: ./services/matching-service
    environment:
      ASPNETCORE_ENVIRONMENT: Development
      DATABASE_URL: Host=postgres-matching;Port=5432;Database=matching_db;Username=postgres;Password=${POSTGRES_PASSWORD};
      RABBITMQ_URL: amqp://${RABBITMQ_DEFAULT_USER}:${RABBITMQ_DEFAULT_PASS}@rabbitmq:5672
      SEQ_URL: http://seq:80
    depends_on:
      rabbitmq:
        condition: service_healthy
      postgres-matching:
        condition: service_started

  aggregation-service:
    build: ./services/aggregation-service
    environment:
      ASPNETCORE_ENVIRONMENT: Development
      DATABASE_URL: Host=postgres-aggregation;Port=5432;Database=aggregation_db;Username=postgres;Password=${POSTGRES_PASSWORD};
      RABBITMQ_URL: amqp://${RABBITMQ_DEFAULT_USER}:${RABBITMQ_DEFAULT_PASS}@rabbitmq:5672
      SEQ_URL: http://seq:80
    depends_on:
      rabbitmq:
        condition: service_healthy
      postgres-aggregation:
        condition: service_started

  notification-service:
    build: ./services/notification-service
    environment:
      ASPNETCORE_ENVIRONMENT: Development
      DATABASE_URL: Host=postgres-notifications;Port=5432;Database=notifications_db;Username=postgres;Password=${POSTGRES_PASSWORD};
      RABBITMQ_URL: amqp://${RABBITMQ_DEFAULT_USER}:${RABBITMQ_DEFAULT_PASS}@rabbitmq:5672
      SEQ_URL: http://seq:80
    depends_on:
      rabbitmq:
        condition: service_healthy
      postgres-notifications:
        condition: service_started

  react-app:
    build: ./frontend/react-app
    ports:
      - "3000:3000"
    environment:
      VITE_API_BASE_URL: http://localhost:5000

volumes:
  users-data:
  receipts-data:
  deals-data:
  matching-data:
  aggregation-data:
  notifications-data:
```

---

## Deployment to External Server

### Option 1: Docker Compose on VPS (Recommended for MVP)

**Requirements:**
- VPS with 4GB RAM, 2 CPU minimum
- Docker and docker-compose installed
- Domain name + SSL certificate (Let's Encrypt)

**Steps:**

```bash
ssh user@your-server.com
git clone <repo-url> expense-tracker
cd expense-tracker

# Create production .env (generate secrets properly)
cat > .env << EOF
RABBITMQ_DEFAULT_USER=secure-user
RABBITMQ_DEFAULT_PASS=$(openssl rand -base64 32)
POSTGRES_PASSWORD=$(openssl rand -base64 32)
JWT_SECRET=$(openssl rand -base64 48)
GOOGLE_CLIENT_ID=your-production-client-id
GOOGLE_CLIENT_SECRET=your-production-client-secret
GOOGLE_REDIRECT_URI=https://your-domain.com/auth/callback
ASPNETCORE_ENVIRONMENT=Production
EOF

# SSL
sudo certbot certonly --standalone -d your-domain.com

# Start
docker-compose -f infrastructure/docker-compose.prod.yml up -d
```

### Option 2: Kubernetes (Future)

Kubernetes manifests will live in `infrastructure/k8s/`. See that directory when ready.

---

## CI/CD Pipeline

`.github/workflows/build-and-test.yml` runs on every push and PR:

1. Run `dotnet test` for all services
2. Run `npm test` for the React app
3. Build all Docker images
4. On merge to `main`: push images to registry

All CI workflow files live in `.github/workflows/` at the repository root (not per-service). Services are differentiated by path filters.

---

## Developer Workflow

```bash
# Start everything
docker-compose up -d

# Watch all logs
docker-compose logs -f

# Watch a specific service
docker-compose logs -f user-service

# Rebuild a single service after code changes
docker-compose up -d --build user-service

# Run tests for a service
docker-compose exec user-service dotnet test

# Create a new EF migration (from host, not container)
cd services/user-service
dotnet ef migrations add YourMigrationName

# Stop everything (keep volumes)
docker-compose down

# Stop and delete all data
docker-compose down -v
```

---

## Pre-Launch Checklist

### Security
- [ ] JWT secrets generated with `openssl rand -base64 48` (not "dev-secret")
- [ ] Database passwords are strong random strings
- [ ] RabbitMQ credentials changed from defaults
- [ ] SSL certificates obtained
- [ ] CORS configured (only allow production frontend domain)
- [ ] Google OAuth redirect URI set to production URL in Google Cloud Console
- [ ] No secrets in git history

### Infrastructure
- [ ] `docker-compose up` tested locally without errors
- [ ] All `/health` endpoints return 200
- [ ] EF migrations run automatically on startup
- [ ] RabbitMQ queues created on startup
- [ ] Seq receiving logs from all services
- [ ] `.env.example` documents all required variables

### Code Quality
- [ ] All services have unit tests
- [ ] No hardcoded secrets in code
- [ ] API documentation (Swagger) up-to-date

### Documentation
- [ ] README with setup instructions
- [ ] `.env.example` complete
