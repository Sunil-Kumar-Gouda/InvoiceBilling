#!/usr/bin/env bash
set -euo pipefail

echo "Bootstrapping LocalStack resources..."

BUCKET_NAME="${INVOICEBILLING_S3_BUCKET:-invoicebilling-invoices}"
QUEUE_NAME="${INVOICEBILLING_SQS_QUEUE:-invoicebilling-jobs}"

awslocal s3 mb "s3://${BUCKET_NAME}" || true
awslocal sqs create-queue --queue-name "${QUEUE_NAME}" >/dev/null || true

echo "Done: S3 bucket + SQS queue created. Bucket=${BUCKET_NAME}, Queue=${QUEUE_NAME}"
