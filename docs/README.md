# AWS upskilling – TypeScript CDK layout

This repo uses **TypeScript** and **AWS CDK v2** for infrastructure and CI/CD. Application Lambdas for the media processor live under `lambda/` (Node.js, ESM).

**Documentation index**

| Doc | Use when |
|-----|----------|
| [COURSE_SETUP.md](COURSE_SETUP.md) | First-time machine setup: Node, AWS CLI, CDK bootstrap, credentials |
| [PROJECT_README.md](PROJECT_README.md) | Architecture overview and repository map (short) |
| This file ([README.md](README.md)) | Deploy commands, HTTP API, pipeline, CloudWatch, stage overrides |
| [PROJECT_SPECIFICATION.md](../PROJECT_SPECIFICATION.md) | Course requirements and optional enhancements |

## Layout

| Path | Role |
|------|------|
| `infrastructure/` | Media processor CDK app (`MediaProcessorStack-SMihajlovic`) — S3, EventBridge, DynamoDB, API Gateway, Lambdas, **ECS admin dashboard** |
| `infrastructure/lib/` | Stack entry (`infrastructure-stack.ts`) composes constructs: `MediaStorage`, `UploadLambda`, `StatusLambda`, `ProcessingPipeline`, **`NotificationMessaging`** (SNS completion topic → SQS → SES notify Lambda), `MediaApi`, `MediaDashboard`, `MediaAdminDashboard` (ECS Fargate + ALB + CloudFront HTTPS), `MediaAlarms`; shared names/retention in `lambda-assets.ts` |
| `admin-dashboard/` | Optional admin UI: **Express** BFF (queries DynamoDB GSI) + **React + Vite** static assets; Docker image built by CDK for Fargate |
| `lambda/` | Upload, processing (Sharp), and **status** handlers; Vitest unit tests (`npm test` in `lambda/`) |
| `ci-cd/pipeline/` | CI/CD CDK app — CodePipeline + CodeBuild + GitHub (CodeStar Connections); artifacts S3 bucket |
| `buildspec.yml` (repo root) | CodeBuild: Lambda Vitest → `cdk deploy` for `infrastructure/` (synth runs inside deploy) |
| `buildspec.destroy.yml` (repo root) | CodeBuild: `cdk destroy` for the media processor stack (used only by the destroy CodePipeline) |
| `docs/` | Project documentation |

### Media processor HTTP API (after deploy)

Outputs include the API base URL (stage `dev`). Typical routes:

| Method | Path | Purpose |
|--------|------|--------|
| `POST` | `/api/upload-image` | Binary image body with **`Content-Type`** one of `image/jpeg`, `image/jpg`, `image/png`, `image/gif`, `image/webp`, `image/heic`. Optional **`X-Original-Filename`** (UTF-8) preserves the client file name (basename only); it is stored on the S3 object and copied into DynamoDB as **`originalFileName`**. Optional **`X-Notify-Email`**: if it looks like a valid email, it is stored as **`notifyEmail`** on the job and used after processing completes (**SNS → SQS → Lambda → SES**). On success, JSON includes `jobId` (and `originalFileName` / `notifyEmail` when those headers were sent). **`415`** + `{ "errorMessage": "..." }` if the type is not supported (e.g. PDF). |
| `GET` | `/api/status/{jobId}` | Job record from DynamoDB (`PENDING`, `PROCESSING`, `COMPLETED`, `FAILED`, …) or `404` only for an unknown id |

After a successful upload, the **upload Lambda** writes a **`PENDING`** row with **`uploadTime`** (ISO 8601) and S3/file fields. The **processing Lambda** then moves the job to **`PROCESSING`** and later **`COMPLETED`** or **`FAILED`**.

Job items include **`uploadTime`**, **`contentType`** (MIME type from upload), **`sizeBytes`**, **`originalFileName`** (`unknown` if no header), optional **`notifyEmail`**, plus **`processingStartedAt`** / **`completedAt`** and thumbnail fields when complete.

The processing Lambda applies the configurable **demo delay** after marking the job `PROCESSING` and **before** reading the object and generating the thumbnail.

### Completion email (SNS → SQS → SES)

After **`COMPLETED`** or **`FAILED`**, if the job has **`notifyEmail`**, the processing Lambda **publishes** a small JSON message to an **SNS** topic. The topic is subscribed to an **SQS** queue; the **`media-processor-smiha-notify-email`** Lambda consumes messages and sends mail with **SES** (`SendEmail`) using a **multipart** body (**HTML** + plain text). The HTML part is styled to match the **admin-dashboard** light theme (colors aligned with `admin-dashboard/client/src/index.css`); clients that prefer text still get the plain body. **`completedAt`** is shown using **`Date#toLocaleString()`** in the Lambda environment (same approach as the admin table’s completed column), not raw ISO-8601. The footer line follows the same pattern as **`AdminFooter`** (`© <year> Media Processor · … · …`), with segments describing the notification (**job completion**) and channel (**Amazon SES**).

- **`SES_FROM_ADDRESS`**: set at deploy time from CDK context **`sesFromAddress`** (must be a **verified identity** in **Amazon SES** in the same Region as the stack).
- **To**: `notifyEmail` from the message (must be verified while SES is in **sandbox**).

Set the verified sender in **`infrastructure/cdk.json`** under **`context.sesFromAddress`**, or override on the CLI:

```bash
npx cdk deploy -c sesFromAddress=noreply@your-verified-domain.example
```

An empty **`sesFromAddress`** (the default in `cdk.json`) means the notification Lambda is deployed with an empty **`SES_FROM_ADDRESS`** and **skips sending** (messages are still delivered to the queue and acknowledged so nothing blocks the pipeline).

Stack outputs **`MediaProcessorProcessingCompletionTopicArn`** and **`MediaProcessorNotificationQueueUrl`** help when debugging the flow.

Completed jobs store **structured fields** in DynamoDB (`demoDelayMs`, `thumbnail` as an object, etc.).

### DynamoDB GSI (admin list)

The jobs table has a global secondary index **`status-uploadTime-index`**: partition key **`status`**, sort key **`uploadTime`** (ISO 8601 strings, same attribute as on the base item). The admin BFF uses it to list and count jobs by status without scanning the whole table. Items **without** both attributes are **not** in the GSI (they still appear in the base table), so the admin list/counts can miss them until **`uploadTime`** exists — the processing Lambda sets it when marking **FAILED** if it was missing. Older FAILED rows may need a one-time **`uploadTime`** backfill in DynamoDB.

### Admin dashboard (ECS Fargate + CloudFront)

After deploy, stack output **`MediaProcessorAdminDashboardUrl`** is the **HTTPS** URL on **CloudFront** (domain like `https://dxxxxxxxxxxxxx.cloudfront.net`), which forwards to an **Application Load Balancer** and then **Fargate** (Express serves the Vite-built React app and JSON APIs). HTTP clients are **redirected to HTTPS**. The ALB security group allows **only** traffic from the **CloudFront managed prefix list** on port 80, so the bare **ALB DNS** is **not** usable from a normal browser or `curl` on the public internet — use the **CloudFront** URL. Output **`MediaProcessorAdminDashboardAlbDns`** is still the ALB hostname (e.g. for AWS support or internal checks).

The browser calls **only** the BFF, not DynamoDB. Endpoints:

| Method | Path | Purpose |
|--------|------|--------|
| `GET` | `/health` | Target group health check (ALB → Fargate) |
| `GET` | `/api/admin/config` | `{ "refreshIntervalMs": number }` — drives UI polling (from container env `ADMIN_REFRESH_INTERVAL_MS`, set by CDK) |
| `GET` | `/api/admin/counts` | Per-status counts and total (paginated `Query` on the GSI with `Select: COUNT`) |
| `GET` | `/api/admin/jobs` | All jobs in the GSI for the table (merged across statuses when no filter, newest by `uploadTime`); the BFF paginates DynamoDB until each partition is fully read. |
| `GET` | `/api/admin/jobs?status=PROCESSING` | Filter to one status, newest first (full partition via paginated queries). |

**Auto-refresh interval (CDK):** set context **`adminRefreshIntervalMs`** (milliseconds, clamped 1000–86400000; default **60000** in `infrastructure/cdk.json`). It is passed to the Fargate container as **`ADMIN_REFRESH_INTERVAL_MS`** (no Docker rebuild needed to change it — only `cdk deploy`). Stack output **`MediaProcessorAdminRefreshIntervalMs`** echoes the value. Example: `npx cdk deploy -c adminRefreshIntervalMs=120000`.

**Synth / deploy** builds the Docker image from `admin-dashboard/Dockerfile` (requires **Docker** on the machine running CDK). Run `cdk deploy` from `infrastructure/` (or the repo root if your app entry is configured accordingly) so `resolveAdminDashboardDir()` can find `admin-dashboard/Dockerfile`.

**Local development** (two terminals; uses your AWS credentials for DynamoDB):

1. Set `JOBS_TABLE_NAME` to the deployed jobs table name and optionally `AWS_REGION`. Optionally set **`ADMIN_REFRESH_INTERVAL_MS`** on the server (same semantics as ECS).
2. From `admin-dashboard/server`: `node index.mjs` (listens on port **3000**).
3. From `admin-dashboard/client`: `npm run dev` — Vite proxies `/api` and `/health` to `http://127.0.0.1:3000`.

The React app entry is `admin-dashboard/client/src/main.tsx`. The tab icon is `admin-dashboard/client/public/favicon.svg` (same gradient and **MP** mark as **`AdminFooter`**). Admin UI code is grouped under **`admin-dashboard/client/src/features/admin-dashboard/`**: **`components/`** (layout and table), **`hooks/`** (data loading, config polling, theme), **`api/adminApi.ts`** (BFF `fetch` helpers), plus shared **`constants`**, **`types`**, and **`lib/guards`** for JSON/env parsing.

If **`/api/admin/config`** cannot be reached (unusual in dev), the UI falls back to optional build-time **`VITE_ADMIN_REFRESH_INTERVAL_MS`** — see `admin-dashboard/client/.env.example`.

The React UI includes a **dark / light theme** toggle. Until you choose a theme, the initial appearance follows **`prefers-color-scheme`**; after toggling, the choice is stored in **`localStorage`** under **`admin-dashboard-theme`** (`dark` or `light`). In the jobs table, each **status** is shown as a color-coded chip (per-status colors are defined in `admin-dashboard/client/src/index.css` for light and dark theme). The **status summary** counts (per-status totals above the table) use the same chip colors. The jobs list is **paginated** in the browser (rows per page, previous/next, and range text); the **rows per page** choice is stored in **`localStorage`** under **`admin-dashboard-jobs-page-size`** (same pattern as the theme key **`admin-dashboard-theme`**). The **uploadTime** and **completed** columns parse ISO timestamps and display them with **`Date#toLocaleString()`** (same style as **Last updated** in the toolbar); hovering shows the original string. The **size** column shows **`sizeBytes`** as **MB** (binary, 1024²); hovering shows the value in bytes.

The public admin URL uses **HTTPS to CloudFront**; there is **no** built-in app authentication (fine for learning; use network controls, AWS WAF, or add auth for real environments).

## Prerequisites

- Node.js 20+ (LTS recommended)
- AWS CLI configured (`aws configure` or SSO)
- CDK bootstrap once per account/region: `cdk bootstrap aws://ACCOUNT/REGION`

## Media processor stack (`infrastructure/`)

```bash
cd infrastructure
npm install
npm run deploy
```

Ensure dependencies for bundled Lambdas are installed where required (e.g. `npm install` in `lambda/status-handler`, `lambda/processing-handler`, and `lambda/notification-handler` so `package-lock.json` is present for `NodejsFunction` bundling).

**Lambda architecture** is **x86_64** in the CDK constructs so Docker bundling uses `linux/amd64`, which matches default **x86** CodeBuild projects. Using **arm64** Lambdas would make CDK bundle with `linux/arm64` and often fail on x86 CodeBuild with an `exec format error` unless you use ARM build hosts or QEMU.

**Lambda unit tests** (Vitest): from `lambda/`, run `npm install` and `npm test`. Tests live beside each handler as `*.test.mjs`; the AWS SDK is mocked so nothing is sent to AWS. Any AWS SDK package imported by a handler under test must be listed in **`lambda/package.json`** (not only in a handler’s own `package.json`) so `npm ci` in CI resolves modules correctly. Processing tests cover exported helpers (S3 event parsing, metadata) and early handler exits (missing env, unsupported event shape, non-`uploads/` keys).

**S3 lifecycle** (`media-storage-construct.ts`): both buckets **abort incomplete multipart uploads** after 7 days. **Expiration**: objects under `uploads/` (input) and `thumbnails/` (output) are removed after **365** days — change the duration or remove the rules if you need longer retention.

Use `npm run synth` or `npx cdk synth` to validate templates without deploying. The first synth may take several minutes while Docker builds the **admin-dashboard** image for the ECS asset.

Set account/region via environment (typical):

```bash
set CDK_DEFAULT_ACCOUNT=123456789012
set CDK_DEFAULT_REGION=eu-central-1
```

(Linux/macOS: `export` instead of `set`.)

### Stage name

Default stage is `dev` (in `cdk.json` → `context.stage`). Override:

```bash
npx cdk deploy -c stage=prod
```

Media processor stack name pattern: `MediaProcessorStack-SMihajlovic-<stage>` (see `infrastructure/bin/infrastructure.ts`).

## Pipeline app (AWS-native CI/CD, GitHub source)

The pipeline uses **CodeStar Connections** to pull from **GitHub** (not CodeCommit), **CodePipeline** for orchestration, **CodeBuild** to run tests and deploy the media processor CDK stack, and the **S3 artifacts bucket** for pipeline artifacts. A **second pipeline** (`media-processor-<stage>-destroy`) runs **`cdk destroy`** via `buildspec.destroy.yml`; its source action has **`triggerOnPush: false`**, so it does **not** run on git push—use **Release change** in the console when you intend to tear down the app stack.

### One-time: GitHub connection

1. In the AWS console: **Developer Tools** → **Connections** → **Create connection** → **GitHub** → complete the OAuth flow and note the connection **ARN**.
2. Ensure the connection status is **Available** before the pipeline can run.

### Configure context

Set these in `ci-cd/pipeline/cdk.json` under `context`, or pass them on the CLI:

| Context | Meaning |
|--------|---------|
| `githubConnectionArn` | ARN from CodeStar Connections (required) |
| `githubOwner` | GitHub org or user that owns the repo |
| `githubRepo` | Repository name (default in `cdk.json`: `aws-upskilling`) |
| `githubBranch` | Branch to build (e.g. `main`) |
| `stage` | Same meaning as for the media processor stack (`dev` / `prod`); passed to `cdk synth` / `cdk deploy` as `-c stage=…` |

Deploy the pipeline stack (same account/region as the app; bootstrap CDK first):

```bash
cd ci-cd/pipeline
npm install
npm run build
npx cdk deploy -c githubConnectionArn=arn:aws:codestar-connections:REGION:ACCOUNT:connection/UUID -c githubOwner=YOUR_ORG -c githubRepo=aws-upskilling -c githubBranch=main
```

Stack name pattern: `Deployment-Pipeline-Stack-SMihajlovic-<stage>` (see `ci-cd/pipeline/bin/pipeline.ts`).

**What runs in CodeBuild** (`buildspec.yml` at repo root): `lambda`: `npm ci` + `npm test` (Vitest) → `infrastructure`: `npm ci` + **`cdk deploy`** (which synthesizes CloudFormation and publishes assets; no separate `cdk synth` step). Commands use **`$CODEBUILD_SRC_DIR`** so each phase resolves paths from the repository root. The CodeBuild role uses **AdministratorAccess** for CDK deploy; narrow this for real production accounts.

The deploy pipeline **CodeBuild project** uses **`privileged: true`** so Docker can run during `cdk deploy` to build and push the **admin-dashboard** container image (ECS asset). Local deploys from your machine also need Docker for that step.

**Destroy pipeline** (`buildspec.destroy.yml`): `infrastructure`: `npm ci` + **`cdk destroy MediaProcessorStack-SMihajlovic --force -c stage=…`**. No Lambda tests and no Docker (not privileged). Start it only via **Release change** on `media-processor-<stage>-destroy`; empty or retained S3 buckets and similar resources can still block CloudFormation delete until resolved.

**Outputs**: `ArtifactsBucketName`, `PipelineName`, `PipelineConsoleUrl`, `DestroyPipelineName`, `DestroyPipelineConsoleUrl`.

After deploy, push to the configured branch to start the **deploy** pipeline (or run **Release change** in the console). The first successful run deploys `MediaProcessorStack-SMihajlovic-<stage>` if it is not already up to date.

## Order of deployment

1. Bootstrap CDK in the target account/region (if not already done).
2. Create the **CodeStar Connection** to GitHub and set pipeline **context** as above.
3. Deploy **`ci-cd/pipeline`** (creates CodePipeline + CodeBuild + artifacts bucket).
4. Optionally deploy `infrastructure/` manually once; otherwise the **pipeline** deploys it from GitHub on the first run.

## CloudWatch (logs, metrics, dashboard)

After a successful deploy, the stack wires up logging, metrics, a dashboard, and alarms as below.

### Lambda log groups

Each function has an explicit log group under `/aws/lambda/<function-name>` with **7-day** retention (`DEFAULT_LOG_RETENTION` in `infrastructure/lib/lambda-assets.ts`). Constructs: `upload-lambda-construct.ts`, `status-lambda-construct.ts`, `processing-pipeline-construct.ts`, `notification-messaging-construct.ts`.

### Logs Insights — correlate by `jobId`

Job-scoped logs are emitted as **one JSON object per line** from `lambda/shared/job-log.mjs`. When a job id is known, each line includes **`jobId`** and **`correlationId`** (same value) plus **`component`**: `upload`, `process`, `status`, or `notify`. In **CloudWatch Logs Insights**, select the log groups you care about (for example all four Lambda groups above, or **Run query** → **Logs Insights** → choose multiple groups), set the time range, and filter on that id:

```sql
fields @timestamp, @message, @log
| filter @message like /"correlationId":"YOUR-JOB-UUID-HERE"/
| sort @timestamp asc
```

Replace `YOUR-JOB-UUID-HERE` with the lowercase UUID from the upload response or DynamoDB. You can use `"jobId":"..."` in the `like` pattern instead; both fields are written together. Lambda may prefix the line with request metadata; the `like` match still finds the JSON substring. API Gateway **access** logs (`media-processor-smiha-api-access-<region>`) use a different format — for **GET** `/api/status/{jobId}` the path may include the id, but the Lambda job logs are the primary cross-service trace.

### API Gateway access logs

**Access logs** use JSON standard fields and go to a dedicated group: `media-processor-smiha-api-access-<region>`. `MediaApiConstruct` sets `cloudWatchRole: true` so API Gateway can write to CloudWatch Logs.

### S3 request metrics

**Request metrics** are enabled on both input and output buckets (`metrics` in `media-storage-construct.ts`; filter IDs in `lambda-assets.ts`). That adds **per-request** CloudWatch metrics (billable) in addition to the free storage metrics.

### Dashboard (`media-processor-smiha-overview`)

Open **CloudWatch → Dashboards** and select **`media-processor-smiha-overview`** (`media-dashboard-construct.ts`). The stack output **`MediaProcessorDashboardName`** repeats this name.

- **Layout**: `autoLayout: true`, `periodOverride: auto`, each graph widget is **quarter-width** (6 of 24 grid units).
- **Lambda row**: invocations, errors & throttles (**bar** charts), duration p99, memory utilization (`MemoryUtilization`, max).
- **Custom metrics** (namespace `media-processor-smiha/Processing`, emitted from `lambda/upload-handler/index.mjs` and `lambda/processing-handler/index.mjs` via `PutMetricData`):
  - `ProcessingDurationMs`, `InputObjectSizeBytes`
  - `ProcessingStatus` by dimension: **PENDING** (upload), **PROCESSING**, **COMPLETED**, **FAILED**
- **Other widgets**: S3 **AllRequests** (request metrics); API Gateway request counts, latency, 4xx/5xx; DynamoDB consumed capacity, user errors, **throttled requests** (all operations); S3 **BucketSizeBytes** and **NumberOfObjects** (StandardStorage — daily storage metrics).

### Alarms (`media-alarms-construct.ts`)

- Lambda **errors** (upload, status, processing)
- API Gateway **5xx**
- DynamoDB **throttled requests** (aggregated for GetItem, PutItem, UpdateItem)
- Custom metric **ProcessingStatus** = **FAILED**

CloudWatch **alarms** still have **no SNS actions** by default — subscribe in the console or extend the construct if you want alarm emails or Chatbot.

## Next steps

- Optional: subscribe **SNS** or **Chatbot** to CloudWatch **alarms** or pipeline failures; **Logs Insights** saved queries in the console.
- Optional: replace **AdministratorAccess** on the CodeBuild role with least-privilege IAM for `cdk deploy`.
