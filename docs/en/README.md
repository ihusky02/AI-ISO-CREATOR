# AI-ISO-Creator Technical Documentation

Welcome to the official technical documentation for **AI-ISO-Creator** — a modern, modular desktop-web application integrated with Google Gemini AI to generate custom bootable Linux ISO images through natural language prompts.

## Table of Contents
1. [System Architecture](./architecture.md)
2. [Gemini AI Integration](./ai-integration.md)
3. [ISO Build Engine and Podman Containerization](./iso-builder.md)

## Key Concepts
- **AI-Guided Generation**: Natural language user intent is transformed into structured JSON recipes (`IsoRecipe`).
- **Clean Separation of Concerns**: Core domain logic, minimal API backend, container-based ISO builder, and lightweight web-desktop frontend.
- **Rootless Build Safety**: Isolated compilation inside rootless Podman containers without host system contamination or root privileges.
- **Real-Time Visibility**: Build logs and step progressions streamed to the client using Server-Sent Events (SSE).
