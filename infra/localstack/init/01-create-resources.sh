#!/usr/bin/env bash
set -euo pipefail

echo "Bootstrapping LocalStack resources..."

awslocal s3 mb s3://invoicebilling-invoices || true
awslocal sqs create-queue --queue-name invoicebilling-jobs >/dev/null || true

echo "Done: S3 bucket + SQS queue created."
