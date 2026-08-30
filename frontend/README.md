# CEBAS Frontend (Next.js & TypeScript)

This is the web client application for **CEBAS (Celoteh Bebas)**, built with Next.js (App Router), TypeScript strict mode, Tailwind CSS, Zustand, and TanStack Query.

## Directory Structure

```
frontend/
├── app/
│   ├── layout.tsx         # Root layout (Inter font, QueryProvider, ToastContainer)
│   ├── page.tsx           # Phase 0 verification & component showcase portal
│   └── globals.css        # Semantic design tokens & Tailwind utilities
├── components/
│   └── ui/                # Accessible WCAG 2.2 AA UI primitives
│       ├── button.tsx     # Button (variants, sizes, loading spinner)
│       ├── input.tsx      # Form input with aria and focus ring
│       ├── modal.tsx      # Accessible dialog (keyboard trap, esc dismiss)
│       ├── dropdown.tsx   # Action menu dropdown
│       ├── skeleton.tsx   # Placeholder loading animation
│       └── toast.tsx      # Toast notifications
├── hooks/
│   └── useToast.ts        # Reusable toast dispatch hook
├── lib/
│   ├── api/
│   │   ├── client.ts      # Fetch API client with timeout & error normalization
│   │   ├── errors.ts      # ProblemDetailsException & network error classes
│   │   └── types.ts       # ProblemDetails, ApiResponse, CursorPagination types
│   └── utils.ts           # Classnames helper (clsx + twMerge)
├── providers/
│   └── query-provider.tsx # TanStack QueryClientProvider
├── stores/
│   └── useUiStore.ts      # Zustand state store for UI and overlays
├── types/
│   ├── api.ts             # API response contracts
│   └── pagination.ts      # Cursor pagination contracts
├── scripts/
│   └── generate-api-types.js # OpenAPI -> TypeScript synchronization script
├── package.json
├── tsconfig.json
└── tailwind.config.ts
```

## Getting Started

### Prerequisites

- Node.js 18+ (Node 25+ recommended)
- npm or pnpm

### Installation

```bash
cd frontend
npm install
```

### Run Development Server

```bash
npm run dev
```

Open [http://localhost:3000](http://localhost:3000) in your browser.

### Linting & Formatting

```bash
npm run lint
npm run format
```

### Production Build

```bash
npm run build
```

## API Client & RFC 7807 Error Handling

The application communicates with the .NET backend via `frontend/lib/api/client.ts`. All error responses are automatically parsed into `ProblemDetailsException` instances containing standard RFC 7807 properties:
- `title`
- `status`
- `detail`
- `instance`
- `traceId`
- `errors` (validation dictionary)
