# Deployment Plan

This document explains how to run InvoiceBilling locally and how CI validates changes.
It also outlines a future path for cloud deployment (Azure/AWS) without requiring it today.

---

## 1. Prerequisites (Local Dev)
- .NET SDK 8.x installed
- Node.js (recommended: 18+ or 20)
- Git
- (Optional) Docker Desktop for future local cloud emulation

---

## 2. Clone and Run (Local)

### 2.1 Clone
```bash
git clone https://github.com/Sunil-Kumar-Gouda/InvoiceBilling.git
cd InvoiceBilling
