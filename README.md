# AI Language Tutor 🌍🎓

An intelligent, AI-powered language learning platform that combines Azure Communication Services, Azure Cognitive Services, and Google Gemini AI to provide an immersive, interactive video-based learning experience.

## 🎞️ Demo Shots

https://github.com/user-attachments/assets/61def2b5-9411-4005-b772-33d43988757c

![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)
![Azure](https://img.shields.io/badge/Azure-Cloud-0078D4?logo=microsoft-azure)
![License](https://img.shields.io/badge/license-MIT-green)

## 🚀 Features

- **AI-Powered Conversations**: Real-time AI tutoring powered by Google Gemini AI
- **Video Learning**: Interactive video sessions with AI avatars using D-ID integration
- **Speech Recognition & Synthesis**: Azure Cognitive Services for speech-to-text and text-to-speech
- **Real-time Communication**: Azure Communication Services for high-quality video calls
- **Adaptive Learning**: Personalized learning paths based on user progress and performance
- **Speech Analysis**: AI-driven pronunciation and grammar correction
- **Progress Tracking**: Comprehensive analytics and learning session history
- **Authentication & Security**: JWT-based authentication with secure user management
- **Modern UI**: Responsive, dark/light theme-enabled interface

## 🛠️ Tech Stack

### Backend
- **Framework**: ASP.NET Core 8.0
- **Database**: SQLite with Entity Framework Core
- **Authentication**: JWT Bearer Tokens
- **APIs**: RESTful Web APIs

### Azure Services
- **Azure Communication Services**: Video calling and real-time communication
- **Azure Cognitive Services (Speech)**: Speech recognition and synthesis
- **Azure Identity**: Secure token generation

### AI & Machine Learning
- **Google Gemini AI**: Natural language processing and conversation analysis
- **D-ID**: AI avatar generation and video synthesis

### Frontend
- **HTML5/CSS3/JavaScript**: Modern, responsive UI
- **Azure Communication Services SDK**: Browser-based video calling
- **Azure Speech SDK**: Client-side speech recognition

## 📋 Prerequisites

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Git](https://git-scm.com/downloads)
- Azure Account with the following services:
  - Azure Communication Services
  - Azure Cognitive Services (Speech)
- Google Gemini AI API Key
- D-ID API Key (for avatar features)

## ⚙️ Installation & Setup

### 1. Clone the Repository

```bash
git clone https://github.com/tahirkhan05/ai-tutor.git
cd ai-tutor
```

### 2. Configure Application Settings

Update `appsettings.json` with your API keys and connection strings:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=languagetutor.db"
  },
  "Jwt": {
    "Key": "YOUR_SECRET_KEY_HERE_AT_LEAST_32_CHARACTERS",
    "Issuer": "AILanguageTutor",
    "Audience": "AILanguageTutorUsers"
  },
  "AzureCommunicationServices": {
    "ConnectionString": "YOUR_ACS_CONNECTION_STRING"
  },
  "GeminiAI": {
    "ApiKey": "YOUR_GEMINI_API_KEY",
    "Model": "gemini-2.5-flash"
  },
  "AzureSpeech": {
    "Key": "YOUR_AZURE_SPEECH_KEY",
    "Region": "YOUR_AZURE_REGION"
  },
  "DID": {
    "ApiKey": "YOUR_DID_API_KEY",
    "ApiUrl": "https://api.d-id.com",
    "DefaultPresenter": "YOUR_AVATAR_IMAGE_URL"
  }
}
```

### 3. Restore Dependencies

```bash
dotnet restore
```

### 4. Apply Database Migrations

```bash
dotnet ef database update
```

If Entity Framework tools are not installed:
```bash
dotnet tool install --global dotnet-ef
```

### 5. Run the Application

```bash
dotnet run
```

The application will be available at:
- HTTP: `http://localhost:5000`
- HTTPS: `https://localhost:5001`
- Swagger API Documentation: `https://localhost:5001/swagger`

## 📁 Project Structure

```
ai-meetv2/
├── Controllers/           # API Controllers
│   ├── AnalyticsController.cs
│   ├── AuthController.cs
│   ├── CommunicationController.cs
│   ├── DIDController.cs
│   ├── GeminiChatController.cs
│   ├── SessionController.cs
│   └── SpeechController.cs
├── Data/                  # Database Context
│   └── AppDbContext.cs
├── Models/                # Data Models
│   ├── ConversationMessage.cs
│   ├── Correction.cs
│   ├── LearningSession.cs
│   ├── User.cs
│   ├── UserProfile.cs
│   └── UserProgress.cs
├── Services/              # Business Logic
│   ├── AdaptiveLearningService.cs
│   ├── JwtTokenService.cs
│   └── PasswordHasher.cs
├── wwwroot/               # Static Files
│   ├── css/
│   ├── js/
│   ├── lib/
│   ├── dashboard.html
│   ├── index.html
│   └── login.html
├── appsettings.json       # Configuration
├── Program.cs             # Application Entry Point
└── ai-meetv2.csproj       # Project File
```

## 🔑 Key Features Explained

### Authentication System
- User registration and login
- JWT token-based authentication
- Secure password hashing
- Protected API endpoints

### AI Chat & Analysis
- Real-time conversation with AI tutor
- Grammar and pronunciation correction
- Contextual learning suggestions
- Progress tracking per session

### Video Communication
- Azure Communication Services integration
- High-quality video calling
- Screen sharing capabilities
- Real-time audio/video streaming

### Speech Services
- Speech-to-text transcription
- Text-to-speech synthesis
- Multi-language support
- Pronunciation analysis

### Analytics & Progress
- Learning session history
- Performance metrics
- Adaptive difficulty adjustment
- Personalized learning recommendations

## 🌐 API Endpoints

### Authentication
- `POST /api/auth/register` - Register new user
- `POST /api/auth/login` - User login

### Chat & AI
- `POST /api/geminichat/analyze` - Analyze speech for corrections
- `POST /api/geminichat/chat` - Interactive chat with AI tutor

### Communication
- `POST /api/communication/token` - Get ACS token
- `GET /api/communication/user/{userId}` - Get user communication identity

### Speech
- `POST /api/speech/token` - Get Azure Speech token

### Sessions
- `POST /api/session` - Create learning session
- `GET /api/session/{sessionId}` - Get session details
- `PUT /api/session/{sessionId}/end` - End session

### Analytics
- `GET /api/analytics/progress/{userId}` - Get user progress
- `GET /api/analytics/sessions/{userId}` - Get user sessions

## 🔐 Security Considerations

⚠️ **Important**: The current `appsettings.json` contains example API keys. For production:

1. **Never commit real API keys** to version control
2. Use **Azure Key Vault** or environment variables for secrets
3. Add `appsettings.json` to `.gitignore`
4. Use **User Secrets** for development:
   ```bash
   dotnet user-secrets init
   dotnet user-secrets set "GeminiAI:ApiKey" "your-key-here"
   ```

## 🚀 Deployment

### Azure App Service Deployment

1. Create an Azure App Service (Windows/.NET 8)
2. Configure Application Settings in Azure Portal
3. Deploy using Visual Studio, VS Code, or Azure CLI:

```bash
az webapp up --name your-app-name --resource-group your-resource-group
```

## 🤝 Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

1. Fork the repository
2. Create your feature branch (`git checkout -b feature/AmazingFeature`)
3. Commit your changes (`git commit -m 'Add some AmazingFeature'`)
4. Push to the branch (`git push origin feature/AmazingFeature`)
5. Open a Pull Request

## 📝 License

This project is licensed under the MIT License - see the LICENSE file for details.

## 🙏 Acknowledgments

- Microsoft Azure for cloud services
- Google Gemini AI for conversational AI
- D-ID for AI avatar technology
- The .NET community for excellent documentation and support

## 📧 Contact

For questions or support, please open an issue on GitHub or contact the maintainer.

## 🔗 Links

- [Azure Communication Services Documentation](https://docs.microsoft.com/azure/communication-services/)
- [Azure Cognitive Services Speech](https://docs.microsoft.com/azure/cognitive-services/speech-service/)
- [Google Gemini AI](https://ai.google.dev/)
- [ASP.NET Core Documentation](https://docs.microsoft.com/aspnet/core/)

---

Made with ❤️ and powered by Azure & AI
