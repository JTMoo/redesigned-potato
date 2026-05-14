# Expense Tracker — Microservices

A full-stack expense tracking application built with a microservices architecture. Users can capture receipts, match them against known deals, and view spending aggregations — all backed by independent services that communicate through RabbitMQ.

## Prerequisites

- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (with Compose v2)
- [.NET 10 SDK](https://dotnet.microsoft.com/download) (for local development)
- [Node 20](https://nodejs.org/) (for local frontend development)

## Quick Start

```bash
# 1. Copy the example environment file and fill in your values
cp .env.example .env

# 2. Build images and start all services
docker-compose up --build
```

The frontend will be available at http://localhost:3000.

To run with development hot-reload volumes:

```bash
docker-compose -f docker-compose.yml -f docker-compose.dev.yml up --build
```

## Service URLs

| Service              | URL / Port                                          | Notes                        |
|----------------------|-----------------------------------------------------|------------------------------|
| Frontend             | http://localhost:3000                               | React (Vite) SPA             |
| API Gateway          | http://localhost:8080                               | YARP reverse proxy           |
| User Service         | http://localhost:8081                               | Auth, profiles               |
| Receipt Service      | http://localhost:8082                               | Receipt upload & storage     |
| Deal Service         | http://localhost:8083                               | Deal catalogue               |
| Matching Service     | http://localhost:8084                               | Receipt ↔ deal matching      |
| Aggregation Service  | http://localhost:8085                               | Spending summaries           |
| Notification Service | http://localhost:8086                               | Email / push notifications   |
| RabbitMQ Management  | http://localhost:15672                              | Default: guest / guest       |
| Seq (log UI)         | http://localhost:5341                               | Structured log viewer        |
| MinIO Console        | http://localhost:9001                               | Object storage UI            |
| MinIO API            | http://localhost:9000                               |                              |

### PostgreSQL Databases (host ports)

| Database          | Host Port |
|-------------------|-----------|
| user-db           | 5432      |
| receipt-db        | 5433      |
| deal-db           | 5434      |
| matching-db       | 5435      |
| aggregation-db    | 5436      |
| notification-db   | 5437      |

## Services

### API Gateway (`services/api-gateway`)
YARP-based reverse proxy that handles Google OAuth 2.0 login, issues JWT tokens, and routes authenticated requests to downstream services. This is the single entry point for all client traffic.

### User Service (`services/user-service`)
Manages user profiles and preferences. Persists to `userdb` and publishes `UserRegistered` events for downstream consumers.

### Receipt Service (`services/receipt-service`)
Handles receipt image uploads (stored in MinIO), OCR metadata, and the receipt lifecycle. Publishes `ReceiptUploaded` events consumed by the matching service.

### Deal Service (`services/deal-service`)
Maintains a catalogue of deals and promotions. Exposes CRUD endpoints and publishes `DealCreated` / `DealUpdated` events.

### Matching Service (`services/matching-service`)
Listens for `ReceiptUploaded` and `DealCreated` events and attempts to match receipts against known deals. Publishes `MatchFound` events.

### Aggregation Service (`services/aggregation-service`)
Consumes match and receipt events to build per-user spending summaries, category breakdowns, and savings reports.

### Notification Service (`services/notification-service`)
Listens for various domain events and dispatches email or push notifications to users.

## Project Structure

```
.
├── services/
│   ├── api-gateway/
│   ├── user-service/
│   ├── receipt-service/
│   ├── deal-service/
│   ├── matching-service/
│   ├── aggregation-service/
│   └── notification-service/
├── shared/
│   ├── contracts/          # Shared MassTransit message contracts
│   └── utilities/          # Common helpers (logging, auth, etc.)
├── frontend/
│   └── react-app/
├── docker-compose.yml
├── docker-compose.dev.yml
└── .env.example
```
