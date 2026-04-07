# Smart Docu Mind

Smart Docu Mind is a junior-friendly ASP.NET Core Web API project that lets users upload a PDF or text file, extract its content, and ask questions about the document using multiple AI modes.

The project is designed to show practical backend skills:
- file upload and validation
- PDF/text extraction
- in-memory chat history
- API integration with OpenAI and Ollama
- fallback handling between AI modes
- Swagger-based API testing

## Features

- Upload `.pdf` and `.txt` documents
- Extract and preview document text
- Ask follow-up questions about uploaded content
- Choose between `fast`, `balanced`, and `advanced` AI modes
- Automatically fall back when a higher mode is unavailable
- Show clear failure reasons when a mode fails

## Tech Stack

Technologies used to build this project:

- ASP.NET Core Web API
- .NET 8
- Swagger / OpenAPI
- [PdfPig](https://github.com/UglyToad/PdfPig) for PDF text extraction
- OpenAI API for advanced mode
- Ollama for local fast and balanced modes

## Project Structure

- `Controllers/` API endpoints
- `Models/` request and response models
- `Services/` file processing, chat memory, and AI provider integrations
- `Program.cs` dependency injection and app startup

## AI Modes

- `fast` -> Ollama with `llama3`
- `balanced` -> Ollama with `mistral`
- `advanced` -> OpenAI with `gpt-4o-mini`

Fallback behavior:
- `advanced` falls back to `balanced`, then `fast`
- `balanced` falls back to `fast`

## Prerequisites

Requirements to run this project locally:

- .NET 8 SDK
- Ollama installed locally if you want to use `fast` or `balanced`
- An OpenAI API key if you want to use `advanced`

## Local Setup

1. Clone the repository.
2. Open the project folder.
3. Set your OpenAI API key as an environment variable:

```bash
export OpenAI__ApiKey="your-api-key"
```

4. Start Ollama if you want local models:

```bash
ollama serve
ollama pull mistral
ollama pull llama3
```

5. Run the API:

```bash
dotnet run
```

6. Open Swagger:

- [http://localhost:5190/swagger](http://localhost:5190/swagger)

## Example API Flow

1. Upload a document using `POST /api/upload`
2. Copy the returned `fileId`
3. Ask a question using `POST /api/upload/ask-stream`

You can also use the included [smart-docu-mind.http](./smart-docu-mind.http) file for quick local testing.

## What I Learned

- how to build a Web API in ASP.NET Core
- how to extract text from uploaded documents
- how to stream AI responses back to the client
- how to structure provider-based services with dependency injection
- how to design fallback logic for AI integrations

## Future Improvements

- persistent storage instead of in-memory chat history
- semantic search or embeddings for better retrieval
- authentication and per-user document history
- unit and integration tests
- a frontend UI for document upload and chat
