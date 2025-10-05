// AI Language Tutor - Modern UI with Dark/Light Mode
// Use Microsoft Speech SDK namespace
const SpeechSDK = window.SpeechSDK || window.Microsoft?.CognitiveServices?.Speech;
// Azure Communication Services SDK - exported as window["azure-communication-calling"]
const ACS = window["azure-communication-calling"] || null;

class LanguageTutorApp {
    constructor() {
        this.isCallActive = false;
        this.conversationHistory = [];
        this.currentLanguage = 'en-US';
        this.speechRecognizer = null;
        this.userStream = null;
        this.currentTheme = localStorage.getItem('theme') || 'dark';
        this.API_BASE = 'http://localhost:5000/api';
        this.token = localStorage.getItem('authToken');
        this.userData = JSON.parse(localStorage.getItem('userData') || '{}');
        this.currentSessionId = null;
        this.sessionStartTime = null;
        this.totalCorrections = 0;
        
        // ACS Video Calling
        this.callClient = null;
        this.callAgent = null;
        this.deviceManager = null;
        this.localVideoStream = null;
        this.videoRenderer = null;
        this.isMuted = false;
        this.isCameraOff = false;
        
        // D-ID Avatar Integration
        this.didEnabled = true;
        this.currentTalkId = null;
        this.didVideoQueue = [];
        this.isPlayingDidVideo = false;
        this.customAvatarUrl = null; // Add this for custom avatar selection
        
        console.log('🎓 Initializing AI Language Tutor...');
        console.log('Speech SDK available:', !!SpeechSDK);
        console.log('ACS SDK available:', !!ACS);
        console.log('Window AzureCommunication:', window.AzureCommunication);
        console.log('Window keys with "Azure":', Object.keys(window).filter(k => k.includes('Azure') || k.includes('ACS') || k.includes('SDK')));
        if (ACS) {
            console.log('✅ ACS SDK loaded with classes:', Object.keys(ACS).filter(k => typeof ACS[k] === 'function'));
        }
        
        this.checkAuthentication();
        this.initializeElements();
        this.attachEventListeners();
        this.applyTheme(this.currentTheme);
        this.updateFeatherIcons();
        this.initializeACS();
    }
    
    async checkAuthentication() {
        // Check if user is logged in
        if (!this.token) {
            console.log('⚠️ Not authenticated, redirecting to login...');
            window.location.href = '/login.html';
            return;
        }
        
        // Verify token
        try {
            const response = await fetch(`${this.API_BASE}/auth/me`, {
                headers: { 'Authorization': `Bearer ${this.token}` }
            });
            
            if (!response.ok) {
                console.log('⚠️ Invalid token, redirecting to login...');
                localStorage.removeItem('authToken');
                localStorage.removeItem('userData');
                window.location.href = '/login.html';
                return;
            }
            
            const userData = await response.json();
            this.userData = userData;
            localStorage.setItem('userData', JSON.stringify(userData));
            console.log('✅ Authenticated as:', userData.username);
            
            // Update UI with user info
            this.updateUserInfo();
        } catch (error) {
            console.error('❌ Authentication check failed:', error);
        }
    }
    
    updateUserInfo() {
        // You can add UI elements to show user name, language, etc.
        if (this.userData.profile && this.userData.profile.targetLanguage) {
            const langCode = this.getLanguageCode(this.userData.profile.targetLanguage);
            if (langCode) {
                this.currentLanguage = langCode;
                this.targetLanguageSelect.value = langCode;
            }
        }
    }
    
    getLanguageCode(langName) {
        const codes = {
            'English': 'en-US',
            'Arabic': 'ar-SA',
            'Telugu': 'te-IN',
            'Tamil': 'ta-IN',
            'Spanish': 'es-ES',
            'French': 'fr-FR',
            'German': 'de-DE',
            'Chinese': 'zh-CN',
            'Japanese': 'ja-JP',
            'Korean': 'ko-KR'
        };
        return codes[langName] || null;
    }
    
    async initializeACS() {
        if (!ACS) {
            console.warn('⚠️ ACS SDK not loaded, video calling will use basic mode');
            return;
        }
        
        try {
            console.log('🎥 Initializing Azure Communication Services...');
            
            // Get ACS token from backend
            const response = await fetch(`${this.API_BASE}/communication/token`, {
                headers: { 'Authorization': `Bearer ${this.token}` }
            });
            
            if (!response.ok) {
                throw new Error('Failed to get ACS token');
            }
            
            const data = await response.json();
            console.log('✅ ACS token received');
            
            // Create CallClient with minimal options to avoid logger dependency issues
            this.callClient = new ACS.CallClient({
                diagnostics: {
                    appName: 'AI-Language-Tutor',
                    appVersion: '1.0.0'
                }
            });
            
            console.log('✅ ACS CallClient created');
            
            // Create a simple token credential object
            // The bundle may not have AzureCommunicationTokenCredential exported
            const tokenCredential = {
                token: data.token,
                getToken: async () => ({ 
                    token: data.token, 
                    expiresOnTimestamp: Date.now() + 3600000 
                })
            };
            
            this.callAgent = await this.callClient.createCallAgent(tokenCredential, {
                displayName: this.userData.username || 'Language Learner'
            });
            
            console.log('✅ ACS CallAgent created');
            
            // Get device manager
            this.deviceManager = await this.callClient.getDeviceManager();
            await this.deviceManager.askDevicePermission({ video: true, audio: true });
            
            console.log('✅ Device permissions granted');
            
        } catch (error) {
            console.error('❌ ACS initialization failed:', error);
            console.log('📹 Falling back to basic video mode');
        }
    }

    initializeElements() {
        this.startCallBtn = document.getElementById('startCallBtn');
        this.endCallBtn = document.getElementById('endCallBtn');
        this.muteBtn = document.getElementById('muteBtn');
        this.cameraBtn = document.getElementById('cameraBtn');
        this.targetLanguageSelect = document.getElementById('targetLanguage');
        this.userVideo = document.getElementById('userVideo');
        this.chatHistory = document.getElementById('chatHistory');
        this.feedbackContent = document.getElementById('feedbackContent');
        this.sessionSummaryContent = document.getElementById('sessionSummaryContent');
        this.connectionStatus = document.getElementById('connectionStatus');
        this.progressToast = document.getElementById('progressToast');
        this.progressIndicator = document.getElementById('progressIndicator');
        this.sessionTitle = document.getElementById('sessionTitle');
        this.speakingIndicator = document.getElementById('speakingIndicator');
        this.micIndicator = document.getElementById('micIndicator');
        this.conversationPanel = document.getElementById('conversationPanel');
        this.feedbackPanel = document.getElementById('feedbackPanel');
        this.themeToggle = document.getElementById('themeToggle');
        
        // D-ID Elements
        this.tutorVideo = document.getElementById('tutorVideo');
        this.tutorAvatar = document.getElementById('tutorAvatar');
        this.tutorLoading = document.getElementById('tutorLoading');
        this.tutorAvatarImage = document.getElementById('tutorAvatarImage');
        this.tutorAvatarFallback = document.getElementById('tutorAvatarFallback');
        
        // Add dashboard button if not exists
        this.addDashboardButton();
    }
    
    addDashboardButton() {
        // Add a dashboard button to the header
        const header = document.querySelector('.header-actions');
        if (header && !document.getElementById('dashboardBtn')) {
            const dashboardBtn = document.createElement('button');
            dashboardBtn.id = 'dashboardBtn';
            dashboardBtn.className = 'icon-btn';
            dashboardBtn.innerHTML = '<i data-feather="bar-chart-2"></i>';
            dashboardBtn.title = 'View Analytics';
            dashboardBtn.addEventListener('click', () => {
                window.location.href = '/dashboard.html';
            });
            header.insertBefore(dashboardBtn, this.themeToggle);
            
            // Add logout button
            const logoutBtn = document.createElement('button');
            logoutBtn.id = 'logoutBtn';
            logoutBtn.className = 'icon-btn';
            logoutBtn.innerHTML = '<i data-feather="log-out"></i>';
            logoutBtn.title = 'Logout';
            logoutBtn.addEventListener('click', () => {
                localStorage.removeItem('authToken');
                localStorage.removeItem('userData');
                window.location.href = '/login.html';
            });
            header.appendChild(logoutBtn);
            
            this.updateFeatherIcons();
        }
    }

    attachEventListeners() {
        this.startCallBtn.addEventListener('click', () => this.startCall());
        this.endCallBtn.addEventListener('click', () => this.endCall());
        this.muteBtn.addEventListener('click', () => this.toggleMute());
        this.cameraBtn.addEventListener('click', () => this.toggleCamera());
        this.targetLanguageSelect.addEventListener('change', (e) => {
            this.currentLanguage = e.target.value;
            console.log('Language changed to:', this.currentLanguage);
        });
        
        // Avatar selector
        const avatarSelector = document.getElementById('avatarSelector');
        if (avatarSelector) {
            // Set initial avatar from first option
            this.customAvatarUrl = avatarSelector.value;
            console.log('Initial avatar set to:', this.customAvatarUrl);
            
            avatarSelector.addEventListener('change', (e) => {
                this.customAvatarUrl = e.target.value;
                console.log('Avatar changed to:', this.customAvatarUrl);
                
                // Update the static avatar image (but don't show it until conversation starts)
                this.updateAvatarImage();
            });
            
            // Load avatar image in background but show emoji placeholder initially
            this.updateAvatarImage();
            this.showEmojiPlaceholder(); // Show emoji until "Start Conversation" is clicked
        }
        
        // Theme toggle
        this.themeToggle.addEventListener('click', () => {
            this.currentTheme = this.currentTheme === 'dark' ? 'light' : 'dark';
            this.applyTheme(this.currentTheme);
        });
    }

    applyTheme(theme) {
        document.documentElement.setAttribute('data-theme', theme);
        localStorage.setItem('theme', theme);
        this.updateFeatherIcons();
    }

    updateFeatherIcons() {
        // Update feather icons after theme change
        setTimeout(() => {
            if (window.feather) {
                feather.replace();
            }
        }, 100);
    }

    async startCall() {
        try {
            console.log('🚀 Starting conversation...');
            
            if (!SpeechSDK) {
                alert('Azure Speech SDK failed to load. Please refresh the page and try again.');
                console.error('❌ SpeechSDK is not defined. Check if the SDK script loaded correctly.');
                return;
            }
            
            console.log('✅ Speech SDK loaded:', typeof SpeechSDK);
            this.updateProgress('Initializing video...');
            this.updateSessionTitle('Initializing conversation...');
            
            // Start ACS Video if available
            await this.startACSVideo();

            // Initialize Azure Speech SDK for voice recognition
            console.log('🎤 Initializing Azure Speech SDK...');
            const speechConfig = await this.getSpeechConfig();
            
            if (!speechConfig) {
                throw new Error('Failed to get speech configuration');
            }

            // Create audio config from microphone
            const audioConfig = SpeechSDK.AudioConfig.fromDefaultMicrophoneInput();
            
            // Create speech recognizer
            this.speechRecognizer = new SpeechSDK.SpeechRecognizer(speechConfig, audioConfig);

            // Setup event handlers
            this.setupSpeechRecognizer();

            // Start continuous recognition
            this.speechRecognizer.startContinuousRecognitionAsync(
                async () => {
                    console.log('✅ Continuous recognition started!');
                    this.isCallActive = true;
                    this.sessionStartTime = new Date();
                    this.totalCorrections = 0;
                    this.startCallBtn.style.display = 'none';
                    this.endCallBtn.style.display = 'inline-flex';
                    this.muteBtn.style.display = 'inline-flex';
                    this.cameraBtn.style.display = 'inline-flex';
                    this.updateConnectionStatus('Connected', 'connected');
                    this.updateProgress('Listening... Start speaking!');
                    this.updateSessionTitle('Active Conversation');
                    this.micIndicator.classList.add('visible');
                    
                    // Replace emoji placeholder with actual avatar image
                    this.showActualAvatar();
                    console.log('✅ Avatar image now visible - conversation started');
                    
                    // Clear chat
                    this.chatHistory.innerHTML = '';
                    this.conversationHistory = [];
                    this.addMessageToChat('system', 'AI Tutor is ready! Just start speaking naturally.');
                    
                    // Initialize live session summary
                    this.updateLiveSessionSummary();
                    
                    // Start backend session tracking
                    await this.startBackendSession();
                    
                    // Update Feather icons
                    this.updateFeatherIcons();
                },
                (err) => {
                    console.error('❌ Error starting recognition:', err);
                    this.updateProgress('Error: ' + err);
                }
            );

        } catch (error) {
            console.error('Error starting call:', error);
            this.updateProgress('Failed to start: ' + error.message);
            this.updateConnectionStatus('Connection failed', 'disconnected');
        }
    }
    
    async startACSVideo() {
        if (!this.deviceManager) {
            throw new Error('ACS is required. Please check your connection and refresh the page.');
        }
        
        console.log('🎥 Starting ACS video stream...');
        
        // Get camera devices
        const cameras = await this.deviceManager.getCameras();
        const camera = cameras[0];
        
        if (!camera) {
            throw new Error('No camera found');
        }
        
        console.log('📹 Using camera:', camera.name);
        
        // Create local video stream
        this.localVideoStream = new ACS.LocalVideoStream(camera);
        
        // Create video stream renderer
        const videoStreamRenderer = new ACS.VideoStreamRenderer(this.localVideoStream);
        this.videoRenderer = videoStreamRenderer;
        
        // Create view
        const view = await videoStreamRenderer.createView({
            scalingMode: 'Crop',
            isMirrored: true
        });
        
        // Attach to video element
        this.userVideo.srcObject = null;
        this.userVideo.style.display = 'none';
        
        // Replace with ACS video element
        const videoContainer = this.userVideo.parentElement;
        if (view.target) {
            view.target.id = 'acsVideoElement';
            view.target.style.width = '100%';
            view.target.style.height = '100%';
            view.target.style.objectFit = 'cover';
            videoContainer.appendChild(view.target);
        }
        
        console.log('✅ ACS video stream started');
    }
    
    async stopACSVideo() {
        try {
            if (this.videoRenderer) {
                this.videoRenderer.dispose();
                this.videoRenderer = null;
                console.log('✅ ACS video renderer disposed');
            }
            
            if (this.localVideoStream) {
                this.localVideoStream = null;
                console.log('✅ ACS video stream stopped');
            }
            
            // Remove ACS video element
            const acsElement = document.getElementById('acsVideoElement');
            if (acsElement) {
                acsElement.remove();
            }
            
        } catch (error) {
            console.error('Error stopping ACS video:', error);
        }
    }

    async getSpeechConfig() {
        try {
            console.log('🔍 Fetching speech config from /api/speech/config...');
            
            // Get speech key and region from backend
            const response = await fetch('/api/speech/config');
            
            console.log('📡 Response status:', response.status);
            
            if (!response.ok) {
                const errorText = await response.text();
                console.error('❌ Failed to get config:', response.status, errorText);
                throw new Error(`Failed to get config: ${response.status}`);
            }
            
            const data = await response.json();
            console.log('✅ Config received:', data);
            
            if (!data.key && !data.Key) {
                throw new Error('No key in config response');
            }
            
            // Handle both lowercase and uppercase property names
            const key = data.key || data.Key;
            const region = data.region || data.Region;
            
            console.log('🔑 Using region:', region);
            
            const speechConfig = SpeechSDK.SpeechConfig.fromSubscription(key, region);
            speechConfig.speechRecognitionLanguage = this.currentLanguage;
            
            console.log('✅ Speech config created for language:', this.currentLanguage);
            return speechConfig;
        } catch (error) {
            console.error('❌ Error getting speech config:', error);
            alert('Failed to initialize speech service. Check console for details.');
            return null;
        }
    }

    setupSpeechRecognizer() {
        // Recognizing event - interim results
        this.speechRecognizer.recognizing = (s, e) => {
            console.log('🎤 Recognizing:', e.result.text);
        };

        // Recognized event - final results
        this.speechRecognizer.recognized = (s, e) => {
            if (e.result.reason === SpeechSDK.ResultReason.RecognizedSpeech) {
                const text = e.result.text;
                console.log('✅ Recognized:', text);
                
                if (text && text.trim().length > 2) {
                    this.handleUserSpeech(text.trim());
                }
            } else if (e.result.reason === SpeechSDK.ResultReason.NoMatch) {
                console.log('⚠️ No speech recognized');
            }
        };

        // Canceled event
        this.speechRecognizer.canceled = (s, e) => {
            console.error('❌ Recognition canceled:', e.errorDetails);
            if (e.reason === SpeechSDK.CancellationReason.Error) {
                console.error('Error details:', e.errorDetails);
            }
        };

        // Session events
        this.speechRecognizer.sessionStarted = (s, e) => {
            console.log('🔵 Speech recognition session started');
        };

        this.speechRecognizer.sessionStopped = (s, e) => {
            console.log('🔴 Speech recognition session stopped');
        };
    }

    async handleUserSpeech(text) {
        try {
            // Ignore input if muted
            if (this.isMuted) {
                console.log('🔇 Ignoring input - microphone is muted');
                return;
            }
            
            // Check for duplicates
            const lastUserMessage = this.conversationHistory
                .filter(msg => msg.isUser)
                .pop();
            
            if (lastUserMessage && lastUserMessage.text === text) {
                console.log('⚠️ Duplicate message, skipping');
                return;
            }

            // Add to chat
            this.addMessageToChat('user', text);
            this.conversationHistory.push({ text, isUser: true, timestamp: new Date() });
            
            // Update live session summary
            this.updateLiveSessionSummary();
            
            // Track message in backend
            await this.trackMessage(text, true);

            // Get AI response
            console.log('💬 Getting AI response...');
            await this.getAIResponse(text);

        } catch (error) {
            console.error('❌ Error handling user speech:', error);
        }
    }

    async getAIResponse(userMessage) {
        try {
            console.log('Calling Gemini API for:', userMessage);
            
            const conversationResponse = await fetch('/api/geminichat/conversation', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    userMessage: userMessage,
                    targetLanguage: this.getLanguageName(this.currentLanguage),
                    topic: 'general conversation',
                    conversationHistory: this.conversationHistory.slice(-10)
                })
            });

            if (conversationResponse.ok) {
                const data = await conversationResponse.json();
                console.log('📦 Full API Response:', data);
                
                const aiResponse = data.Response || data.response;
                const corrections = data.Corrections || data.corrections || [];
                
                console.log('✅ AI Response:', aiResponse);
                console.log('📝 Corrections array:', corrections);
                console.log('📝 Corrections length:', corrections.length);
                console.log('📝 First correction:', corrections[0]);
                
                // Display live feedback
                if (corrections.length > 0) {
                    this.totalCorrections += corrections.length;
                    this.displayCorrections(corrections);
                } else {
                    this.displayFeedback('Great job! No corrections needed. Keep it up! 🎉');
                }
                
                // Add to chat
                this.addMessageToChat('tutor', aiResponse);
                this.conversationHistory.push({ text: aiResponse, isUser: false, timestamp: new Date() });
                
                // Update live session summary
                this.updateLiveSessionSummary();
                
                // Track AI message in backend
                await this.trackMessage(aiResponse, false);
                
                // Track corrections if any
                if (corrections.length > 0) {
                    await this.trackCorrections(corrections, userMessage);
                }

                // Speak the response
                await this.speakText(aiResponse);
            } else {
                throw new Error('Failed to get AI response');
            }
        } catch (error) {
            console.error('❌ Error getting AI response:', error);
            this.addMessageToChat('tutor', 'Sorry, I had trouble processing that. Please try again.');
        }
    }
    
    displayFeedback(feedback) {
        if (!this.feedbackContent) return;
        this.feedbackContent.innerHTML = `
            <div class="feedback-item">
                <strong>💬 Live Feedback:</strong>
                <p>${feedback}</p>
                <small>Updated: ${new Date().toLocaleTimeString()}</small>
            </div>
        `;
    }

    displayCorrections(corrections) {
        if (!this.feedbackContent) return;
        
        console.log('🎯 Displaying corrections:', JSON.stringify(corrections, null, 2));
        
        const correctionsHtml = corrections.map((c, index) => {
            console.log(`Correction ${index}:`, c);
            
            // Try all possible property name variations
            const severity = (c.Severity || c.severity || 'Low');
            const errorType = c.ErrorType || c.errorType || 'Error';
            const original = c.OriginalText || c.originalText || c.incorrectText || c.IncorrectText || '';
            const corrected = c.CorrectedText || c.correctedText || c.correctText || c.CorrectText || '';
            const explanation = c.Explanation || c.explanation || '';
            
            console.log(`Mapped values for correction ${index}:`, {
                severity,
                errorType,
                original: original || 'EMPTY',
                corrected: corrected || 'EMPTY',
                explanation
            });
            
            const severityLower = severity.toLowerCase();
            
            return `
                <div class="feedback-item correction-item severity-${severityLower}">
                    <div class="correction-header">
                        <strong>${errorType}</strong>
                        <span class="severity-badge badge-${severityLower}">${severity.toUpperCase()}</span>
                    </div>
                    <div class="correction-text">
                        <div style="color: #ef4444; margin-bottom: 4px;">❌ ${original || '[No original text]'}</div>
                        <div style="color: #10b981;">✅ ${corrected || '[No corrected text]'}</div>
                    </div>
                    ${explanation ? `<p class="explanation">${explanation}</p>` : ''}
                </div>
            `;
        }).join('');
        
        this.feedbackContent.innerHTML = `
            <div class="feedback-header">
                <strong>🎯 Corrections (${corrections.length}):</strong>
            </div>
            ${correctionsHtml}
            <small class="feedback-time">Updated: ${new Date().toLocaleTimeString()}</small>
        `;
    }
    
    showCorrections(corrections) {
        corrections.forEach(correction => {
            const correctionDiv = document.createElement('div');
            correctionDiv.className = 'message correction-message';
            correctionDiv.innerHTML = `
                <div class="message-content">
                    <strong>📝 ${correction.errorType}:</strong><br>
                    <span style="color: #f56565;">❌ ${correction.incorrectText}</span><br>
                    <span style="color: #48bb78;">✓ ${correction.correctText}</span>
                    ${correction.explanation ? `<br><em>${correction.explanation}</em>` : ''}
                </div>
            `;
            this.chatHistory.appendChild(correctionDiv);
            this.chatHistory.scrollTop = this.chatHistory.scrollHeight;
        });
    }

    async speakText(text) {
        try {
            console.log('🔊 Speaking:', text);
            this.speakingIndicator.classList.add('active');
            
            // Use D-ID for video avatar if enabled
            if (this.didEnabled && this.isCallActive) {
                await this.speakWithDID(text);
            } else {
                // Fallback to audio-only
                await this.speakWithAudio(text);
            }
        } catch (error) {
            console.error('❌ Error speaking:', error);
            this.speakingIndicator.classList.remove('active');
            // Fallback to audio if D-ID fails
            if (this.didEnabled) {
                console.log('Falling back to audio-only...');
                await this.speakWithAudio(text);
            }
        }
    }

    async speakWithDID(text) {
        try {
            console.log('🎬 Creating D-ID video for:', text.substring(0, 50));
            
            // Show loading indicator
            this.showTutorLoading(true);
            
            // Create D-ID talk
            const response = await fetch('/api/did/create-talk', {
                method: 'POST',
                headers: { 
                    'Content-Type': 'application/json',
                    'Authorization': `Bearer ${this.token}`
                },
                body: JSON.stringify({
                    text: text,
                    language: this.currentLanguage,
                    presenterImageUrl: this.customAvatarUrl // Use custom avatar if set
                })
            });

            if (!response.ok) {
                const errorData = await response.json();
                console.error('D-ID API error:', errorData);
                throw new Error('Failed to create D-ID video');
            }

            const data = await response.json();
            this.currentTalkId = data.id;
            console.log('✅ D-ID talk created:', this.currentTalkId);

            // Poll for video completion
            await this.waitForDidVideo(this.currentTalkId);
            
        } catch (error) {
            console.error('❌ D-ID video creation failed:', error);
            this.showTutorLoading(false);
            throw error;
        }
    }

    async waitForDidVideo(talkId, maxAttempts = 30) {
        for (let i = 0; i < maxAttempts; i++) {
            try {
                const response = await fetch(`/api/did/talk/${talkId}`, {
                    headers: { 'Authorization': `Bearer ${this.token}` }
                });

                if (!response.ok) {
                    throw new Error('Failed to check D-ID status');
                }

                const data = await response.json();
                console.log(`🔄 D-ID status (attempt ${i + 1}):`, data.status);

                if (data.status === 'done' && data.result_url) {
                    console.log('✅ D-ID video ready:', data.result_url);
                    await this.playDidVideo(data.result_url);
                    return;
                } else if (data.status === 'error') {
                    throw new Error('D-ID video generation failed');
                }

                // Wait 1 second before next check
                await new Promise(resolve => setTimeout(resolve, 1000));
            } catch (error) {
                console.error('Error checking D-ID status:', error);
                throw error;
            }
        }

        throw new Error('D-ID video generation timeout');
    }

    async playDidVideo(videoUrl) {
        try {
            console.log('▶️ Playing D-ID video:', videoUrl);
            
            // Hide loading, show video
            this.showTutorLoading(false);
            this.hideTutorAvatar();
            
            // Set video source and play
            this.tutorVideo.src = videoUrl;
            this.tutorVideo.style.display = 'block';
            
            // Wait for video to be ready
            await new Promise((resolve, reject) => {
                this.tutorVideo.onloadeddata = resolve;
                this.tutorVideo.onerror = reject;
            });

            await this.tutorVideo.play();
            console.log('✅ D-ID video playing');

            // Handle video end
            this.tutorVideo.onended = () => {
                console.log('✅ D-ID video finished');
                this.speakingIndicator.classList.remove('active');
                this.tutorVideo.style.display = 'none';
                this.showTutorAvatar();
            };

        } catch (error) {
            console.error('❌ Error playing D-ID video:', error);
            this.speakingIndicator.classList.remove('active');
            this.showTutorLoading(false);
            this.showTutorAvatar();
            throw error;
        }
    }

    async speakWithAudio(text) {
        try {
            const response = await fetch('/api/speech/synthesize', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    text: text,
                    language: this.currentLanguage
                })
            });

            if (response.ok) {
                const audioBlob = await response.blob();
                const audioUrl = URL.createObjectURL(audioBlob);
                const audio = new Audio(audioUrl);
                
                audio.onended = () => {
                    this.speakingIndicator.classList.remove('active');
                    URL.revokeObjectURL(audioUrl);
                };
                
                await audio.play();
            }
        } catch (error) {
            console.error('❌ Error with audio speech:', error);
            this.speakingIndicator.classList.remove('active');
        }
    }

    showTutorLoading(show) {
        if (this.tutorLoading) {
            this.tutorLoading.style.display = show ? 'flex' : 'none';
        }
    }

    hideTutorAvatar() {
        if (this.tutorAvatar) {
            this.tutorAvatar.style.display = 'none';
        }
    }

    showTutorAvatar() {
        if (this.tutorAvatar) {
            this.tutorAvatar.style.display = 'flex';
        }
    }

    // Show emoji placeholder (before conversation starts)
    showEmojiPlaceholder() {
        if (this.tutorAvatar) {
            this.tutorAvatar.style.display = 'flex';
        }
        if (this.tutorAvatarImage) {
            this.tutorAvatarImage.style.display = 'none';
        }
        if (this.tutorAvatarFallback) {
            this.tutorAvatarFallback.style.display = 'flex';
        }
        console.log('👩‍🏫 Showing emoji placeholder');
    }

    // Show actual avatar image (after conversation starts)
    showActualAvatar() {
        if (this.tutorAvatar) {
            this.tutorAvatar.style.display = 'flex';
        }
        if (this.tutorAvatarImage) {
            this.tutorAvatarImage.style.display = 'block';
        }
        if (this.tutorAvatarFallback) {
            this.tutorAvatarFallback.style.display = 'none';
        }
        console.log('🖼️ Showing actual avatar image');
    }

    // Update the static avatar image based on current selection
    updateAvatarImage() {
        if (!this.tutorAvatarImage) return;
        
        // Get the current avatar URL from dropdown selection
        let avatarUrl = this.customAvatarUrl;
        
        // If no URL selected, keep showing emoji
        if (!avatarUrl) {
            console.warn('⚠️ No avatar URL selected');
            return;
        }
        
        console.log('Loading avatar image in background:', avatarUrl);
        
        // Set the image source (but don't show it yet - that happens in showActualAvatar)
        this.tutorAvatarImage.src = avatarUrl;
        
        // Handle image load success
        this.tutorAvatarImage.onload = () => {
            console.log('✅ Avatar image loaded and ready');
            // Don't show it automatically - wait for conversation to start
        };
        
        // Handle image load error
        this.tutorAvatarImage.onerror = () => {
            console.warn('⚠️ Failed to load avatar image');
            // Keep showing emoji placeholder
        };
    }

    async endCall() {
        try {
            console.log('🛑 Ending conversation...');
            this.isCallActive = false;
            
            // End backend session
            await this.endBackendSession();
            
            // Stop speech recognition
            if (this.speechRecognizer) {
                this.speechRecognizer.stopContinuousRecognitionAsync(
                    () => {
                        console.log('✅ Recognition stopped');
                        this.speechRecognizer.close();
                        this.speechRecognizer = null;
                    },
                    (err) => {
                        console.error('Error stopping recognition:', err);
                    }
                );
            }
            
            // Stop D-ID video
            this.stopDidVideo();
            
            // Stop ACS video
            await this.stopACSVideo();
            
            // Show emoji placeholder when conversation ends
            this.showEmojiPlaceholder();
            console.log('✅ Back to emoji placeholder - conversation ended');
            
            this.startCallBtn.style.display = 'inline-flex';
            this.endCallBtn.style.display = 'none';
            this.muteBtn.style.display = 'none';
            this.cameraBtn.style.display = 'none';
            this.micIndicator.classList.remove('visible');
            this.updateConnectionStatus('Disconnected', 'disconnected');
            this.updateProgress('Session ended. Click "Start Conversation" to begin again.');
            this.updateSessionTitle('Ready to start learning');
            
            // Reset states
            this.isMuted = false;
            this.isCameraOff = false;
            
            // Update Feather icons
            this.updateFeatherIcons();
            
        } catch (error) {
            console.error('Error ending call:', error);
        }
    }

    stopDidVideo() {
        try {
            // Stop any playing D-ID video
            if (this.tutorVideo) {
                this.tutorVideo.pause();
                this.tutorVideo.src = '';
                this.tutorVideo.style.display = 'none';
            }
            
            // Hide loading
            this.showTutorLoading(false);
            
            // Show avatar
            this.showTutorAvatar();
            
            // Clear current talk ID
            this.currentTalkId = null;
            
            console.log('✅ D-ID video stopped');
        } catch (error) {
            console.error('Error stopping D-ID video:', error);
        }
    }
    
    // Backend Session Tracking Methods
    async startBackendSession() {
        if (!this.token) {
            console.warn('⚠️ No auth token, cannot start backend session');
            return;
        }
        
        try {
            console.log('🚀 Starting backend session...');
            
            const response = await fetch(`${this.API_BASE}/session/start`, {
                method: 'POST',
                headers: {
                    'Authorization': `Bearer ${this.token}`,
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify({
                    targetLanguage: this.getLanguageName(this.currentLanguage),
                    topic: 'General Conversation',
                    mode: 'Casual'
                })
            });
            
            console.log('📡 Session start response:', response.status);
            
            if (response.ok) {
                const data = await response.json();
                this.currentSessionId = data.SessionId || data.sessionId;
                console.log('✅ Backend session started:', this.currentSessionId);
                console.log('📊 Session data:', data);
            } else {
                const errorText = await response.text();
                console.error('❌ Failed to start session:', response.status, errorText);
            }
        } catch (error) {
            console.error('❌ Failed to start backend session:', error);
        }
    }
    
    async trackMessage(message, isUser) {
        if (!this.token || !this.currentSessionId) {
            console.warn('⚠️ Cannot track message: no token or session');
            return;
        }
        
        try {
            console.log('📤 Tracking message:', { sessionId: this.currentSessionId, isUser, message });
            
            const response = await fetch(`${this.API_BASE}/session/${this.currentSessionId}/message`, {
                method: 'POST',
                headers: {
                    'Authorization': `Bearer ${this.token}`,
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify({
                    text: message,  // Backend expects 'text' not 'message'
                    isUser: isUser,
                    language: this.getLanguageName(this.currentLanguage)
                })
            });
            
            if (response.ok) {
                const data = await response.json();
                console.log('✅ Message tracked:', data);
            } else {
                console.error('❌ Failed to track message:', response.status, await response.text());
            }
        } catch (error) {
            console.error('❌ Failed to track message:', error);
        }
    }
    
    async trackCorrections(corrections, originalText) {
        if (!this.token || !this.currentSessionId) {
            console.warn('⚠️ Cannot track corrections: no token or session');
            return;
        }
        
        try {
            for (const correction of corrections) {
                // Handle both PascalCase and camelCase properties
                const errorType = correction.ErrorType || correction.errorType || 'General';
                const originalTextValue = correction.OriginalText || correction.originalText || 
                                        correction.incorrectText || correction.IncorrectText || originalText;
                const correctedTextValue = correction.CorrectedText || correction.correctedText || 
                                          correction.correctText || correction.CorrectText || '';
                const explanation = correction.Explanation || correction.explanation || '';
                const severity = correction.Severity || correction.severity || 'Medium';
                
                console.log('📝 Tracking correction:', {
                    errorType,
                    originalText: originalTextValue,
                    correctedText: correctedTextValue,
                    explanation,
                    severity
                });
                
                const response = await fetch(`${this.API_BASE}/session/${this.currentSessionId}/correction`, {
                    method: 'POST',
                    headers: {
                        'Authorization': `Bearer ${this.token}`,
                        'Content-Type': 'application/json'
                    },
                    body: JSON.stringify({
                        originalText: originalTextValue,  // Backend expects these exact names
                        correctedText: correctedTextValue,
                        errorType: errorType,
                        explanation: explanation,
                        severity: severity
                    })
                });
                
                if (response.ok) {
                    const data = await response.json();
                    console.log('✅ Correction tracked:', data);
                } else {
                    console.error('❌ Failed to track correction:', response.status, await response.text());
                }
            }
            console.log(`✅ Tracked ${corrections.length} corrections`);
        } catch (error) {
            console.error('❌ Failed to track corrections:', error);
        }
    }
    
    async endBackendSession() {
        if (!this.token || !this.currentSessionId) {
            console.warn('⚠️ Cannot end session: no token or session');
            return;
        }
        
        try {
            console.log('🛑 Ending backend session:', this.currentSessionId);
            
            const response = await fetch(`${this.API_BASE}/session/${this.currentSessionId}/end`, {
                method: 'POST',
                headers: {
                    'Authorization': `Bearer ${this.token}`,
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify({
                    accuracyScore: 85.0,  // You can calculate this based on corrections
                    summary: 'Practice session completed'
                })
            });
            
            console.log('📡 End session response:', response.status);
            
            if (response.ok) {
                const summary = await response.json();
                console.log('✅ Session ended, summary:', summary);
                this.showSessionSummary(summary);
            } else {
                const errorText = await response.text();
                console.error('❌ Failed to end session:', response.status, errorText);
            }
            
            this.currentSessionId = null;
        } catch (error) {
            console.error('❌ Failed to end backend session:', error);
        }
    }
    
    showSessionSummary(summary) {
        // Handle both PascalCase and camelCase properties
        const duration = Math.round(summary.DurationMinutes || summary.durationMinutes || 0);
        const messageCount = summary.MessageCount || summary.messageCount || 0;
        const correctionCount = summary.CorrectionCount || summary.correctionCount || 0;
        const accuracy = Math.round(summary.AccuracyScore || summary.accuracyScore || 0);
        const difficulty = summary.DifficultyLevel || summary.difficultyLevel || 'N/A';
        
        // Update the session summary section
        if (this.sessionSummaryContent) {
            this.sessionSummaryContent.innerHTML = `
                <div class="summary-stats">
                    <div class="stat-item">
                        <div class="stat-label">
                            <i data-feather="clock"></i>
                            <span>Duration</span>
                        </div>
                        <div class="stat-value">${duration} min</div>
                    </div>
                    <div class="stat-item">
                        <div class="stat-label">
                            <i data-feather="message-circle"></i>
                            <span>Messages</span>
                        </div>
                        <div class="stat-value">${messageCount}</div>
                    </div>
                    <div class="stat-item">
                        <div class="stat-label">
                            <i data-feather="edit-3"></i>
                            <span>Corrections</span>
                        </div>
                        <div class="stat-value ${correctionCount > 5 ? 'warning' : 'positive'}">${correctionCount}</div>
                    </div>
                    <div class="stat-item">
                        <div class="stat-label">
                            <i data-feather="check-circle"></i>
                            <span>Accuracy</span>
                        </div>
                        <div class="stat-value positive">${accuracy}%</div>
                    </div>
                    <div class="stat-item">
                        <div class="stat-label">
                            <i data-feather="target"></i>
                            <span>Difficulty</span>
                        </div>
                        <div class="stat-value">${difficulty}</div>
                    </div>
                </div>
                <div class="summary-note">
                    🎉 Great session! Keep practicing to improve further.
                </div>
            `;
            this.updateFeatherIcons();
        }
    }
    
    updateLiveSessionSummary() {
        // Update session summary in real-time during conversation
        if (!this.sessionSummaryContent || !this.isCallActive) return;
        
        const messageCount = this.conversationHistory.length;
        const userMessages = this.conversationHistory.filter(m => m.isUser).length;
        const tutorMessages = this.conversationHistory.filter(m => !m.isUser).length;
        const correctionCount = this.totalCorrections || 0;
        
        const now = new Date();
        const sessionStart = this.sessionStartTime || now;
        const duration = Math.round((now - sessionStart) / 60000); // minutes
        
        this.sessionSummaryContent.innerHTML = `
            <div class="summary-stats">
                <div class="stat-item">
                    <div class="stat-label">
                        <i data-feather="clock"></i>
                        <span>Duration</span>
                    </div>
                    <div class="stat-value">${duration} min</div>
                </div>
                <div class="stat-item">
                    <div class="stat-label">
                        <i data-feather="message-circle"></i>
                        <span>Your Messages</span>
                    </div>
                    <div class="stat-value">${userMessages}</div>
                </div>
                <div class="stat-item">
                    <div class="stat-label">
                        <i data-feather="message-square"></i>
                        <span>Tutor Responses</span>
                    </div>
                    <div class="stat-value">${tutorMessages}</div>
                </div>
                <div class="stat-item">
                    <div class="stat-label">
                        <i data-feather="edit-3"></i>
                        <span>Corrections</span>
                    </div>
                    <div class="stat-value ${correctionCount > 5 ? 'warning' : 'positive'}">${correctionCount}</div>
                </div>
            </div>
            <div class="summary-note">
                📊 Live session statistics - Keep going!
            </div>
        `;
        this.updateFeatherIcons();
    }

    addMessageToChat(sender, text) {
        const messageDiv = document.createElement('div');
        messageDiv.className = `message ${sender}-message`;
        
        const messageContent = document.createElement('div');
        messageContent.className = 'message-content';
        messageContent.textContent = text;
        
        const messageTime = document.createElement('div');
        messageTime.className = 'message-time';
        messageTime.textContent = new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
        
        messageDiv.appendChild(messageContent);
        messageDiv.appendChild(messageTime);
        
        this.chatHistory.appendChild(messageDiv);
        this.chatHistory.scrollTop = this.chatHistory.scrollHeight;
    }

    updateConnectionStatus(status, className) {
        const statusText = this.connectionStatus.querySelector('.status-text');
        if (statusText) {
            statusText.textContent = status;
        }
        this.connectionStatus.className = `connection-badge ${className}`;
    }

    updateProgress(message) {
        this.progressIndicator.textContent = message;
        this.progressToast.classList.add('visible');
        
        // Auto-hide after 3 seconds
        clearTimeout(this.progressTimeout);
        this.progressTimeout = setTimeout(() => {
            this.progressToast.classList.remove('visible');
        }, 3000);
    }

    updateSessionTitle(title) {
        if (this.sessionTitle) {
            this.sessionTitle.textContent = title;
        }
    }

    async toggleMute() {
        if (!this.speechRecognizer) return;
        
        this.isMuted = !this.isMuted;
        
        try {
            if (this.isMuted) {
                // Stop recognition when muted
                await new Promise((resolve, reject) => {
                    this.speechRecognizer.stopContinuousRecognitionAsync(
                        () => {
                            console.log('🔇 Microphone muted - recognition stopped');
                            this.muteBtn.classList.add('muted');
                            this.muteBtn.title = 'Unmute microphone';
                            this.muteBtn.innerHTML = '<i data-feather="mic-off"></i>';
                            this.micIndicator.classList.remove('visible');
                            this.updateFeatherIcons();
                            resolve();
                        },
                        (err) => {
                            console.error('Error stopping recognition:', err);
                            reject(err);
                        }
                    );
                });
            } else {
                // Resume recognition when unmuted
                await new Promise((resolve, reject) => {
                    this.speechRecognizer.startContinuousRecognitionAsync(
                        () => {
                            console.log('🔊 Microphone unmuted - recognition resumed');
                            this.muteBtn.classList.remove('muted');
                            this.muteBtn.title = 'Mute microphone';
                            this.muteBtn.innerHTML = '<i data-feather="mic"></i>';
                            this.micIndicator.classList.add('visible');
                            this.updateFeatherIcons();
                            resolve();
                        },
                        (err) => {
                            console.error('Error starting recognition:', err);
                            reject(err);
                        }
                    );
                });
            }
        } catch (error) {
            console.error('Error toggling mute:', error);
            // Revert state on error
            this.isMuted = !this.isMuted;
        }
    }

    async toggleCamera() {
        if (!this.localVideoStream) return;
        
        this.isCameraOff = !this.isCameraOff;
        
        try {
            const acsElement = document.getElementById('acsVideoElement');
            if (acsElement) {
                if (this.isCameraOff) {
                    acsElement.style.display = 'none';
                    this.cameraBtn.classList.add('camera-off');
                    this.cameraBtn.title = 'Turn on camera';
                    this.cameraBtn.innerHTML = '<i data-feather="video-off"></i>';
                    console.log('📹 Camera off');
                } else {
                    acsElement.style.display = 'block';
                    this.cameraBtn.classList.remove('camera-off');
                    this.cameraBtn.title = 'Turn off camera';
                    this.cameraBtn.innerHTML = '<i data-feather="video"></i>';
                    console.log('📹 Camera on');
                }
                this.updateFeatherIcons();
            }
        } catch (error) {
            console.error('Error toggling camera:', error);
        }
    }

    getLanguageName(code) {
        const names = {
            'en-US': 'English',
            'ar-SA': 'Arabic',
            'te-IN': 'Telugu',
            'ta-IN': 'Tamil'
        };
        return names[code] || 'English';
    }
}

// Initialize app when page loads
document.addEventListener('DOMContentLoaded', () => {
    console.log('🎓 Initializing AI Language Tutor with Azure Speech SDK...');
    window.tutorApp = new LanguageTutorApp();
});
