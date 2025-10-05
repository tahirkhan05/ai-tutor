// Authentication JavaScript
const API_BASE_URL = window.location.origin;

class AuthManager {
    constructor() {
        this.initializeElements();
        this.attachEventListeners();
        this.checkExistingAuth();
    }

    initializeElements() {
        this.loginForm = document.getElementById('loginForm');
        this.registerForm = document.getElementById('registerForm');
        this.loginFormElement = document.getElementById('loginFormElement');
        this.registerFormElement = document.getElementById('registerFormElement');
        this.showRegisterBtn = document.getElementById('showRegisterBtn');
        this.showLoginBtn = document.getElementById('showLoginBtn');
        this.loadingOverlay = document.getElementById('loadingOverlay');
        this.loadingMessage = document.getElementById('loadingMessage');
        this.alertMessage = document.getElementById('alertMessage');
        this.alertText = document.getElementById('alertText');
    }

    attachEventListeners() {
        // Form submissions
        this.loginFormElement.addEventListener('submit', (e) => this.handleLogin(e));
        this.registerFormElement.addEventListener('submit', (e) => this.handleRegister(e));

        // Toggle forms
        this.showRegisterBtn.addEventListener('click', () => this.showRegister());
        this.showLoginBtn.addEventListener('click', () => this.showLogin());
    }

    showRegister() {
        this.loginForm.style.display = 'none';
        this.registerForm.style.display = 'block';
        if (window.feather) feather.replace();
    }

    showLogin() {
        this.registerForm.style.display = 'none';
        this.loginForm.style.display = 'block';
        if (window.feather) feather.replace();
    }

    showLoading(message = 'Processing...') {
        this.loadingMessage.textContent = message;
        this.loadingOverlay.style.display = 'flex';
    }

    hideLoading() {
        this.loadingOverlay.style.display = 'none';
    }

    showAlert(message, type = 'info') {
        this.alertMessage.className = `alert-message ${type}`;
        this.alertText.textContent = message;
        this.alertMessage.style.display = 'block';
        
        if (window.feather) feather.replace();

        setTimeout(() => {
            this.alertMessage.style.display = 'none';
        }, 5000);
    }

    checkExistingAuth() {
        const token = localStorage.getItem('authToken');
        if (token) {
            // Verify token is still valid
            this.verifyToken(token);
        }
    }

    async verifyToken(token) {
        try {
            const response = await fetch(`${API_BASE_URL}/api/auth/me`, {
                headers: {
                    'Authorization': `Bearer ${token}`
                }
            });

            if (response.ok) {
                const userData = await response.json();
                // Token is valid, redirect to main app
                window.location.href = '/index.html';
            } else {
                // Token expired, clear it
                localStorage.removeItem('authToken');
                localStorage.removeItem('userData');
            }
        } catch (error) {
            console.error('Token verification error:', error);
            localStorage.removeItem('authToken');
            localStorage.removeItem('userData');
        }
    }

    async handleLogin(event) {
        event.preventDefault();

        const email = document.getElementById('loginEmail').value.trim();
        const password = document.getElementById('loginPassword').value;

        if (!email || !password) {
            this.showAlert('Please fill in all fields', 'error');
            return;
        }

        this.showLoading('Signing in...');

        try {
            const response = await fetch(`${API_BASE_URL}/api/auth/login`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify({ email, password })
            });

            const data = await response.json();

            if (response.ok) {
                // Save token and user data
                localStorage.setItem('authToken', data.token);
                localStorage.setItem('userData', JSON.stringify(data.user));

                this.showAlert('Login successful! Redirecting...', 'success');
                
                setTimeout(() => {
                    window.location.href = '/index.html';
                }, 1500);
            } else {
                this.hideLoading();
                this.showAlert(data.message || data || 'Login failed. Please check your credentials.', 'error');
            }
        } catch (error) {
            this.hideLoading();
            console.error('Login error:', error);
            this.showAlert('Connection error. Please try again.', 'error');
        }
    }

    async handleRegister(event) {
        event.preventDefault();

        const username = document.getElementById('registerUsername').value.trim();
        const email = document.getElementById('registerEmail').value.trim();
        const password = document.getElementById('registerPassword').value;
        const nativeLanguage = document.getElementById('nativeLanguage').value;
        const targetLanguage = document.getElementById('targetLanguage').value;
        const proficiencyLevel = document.getElementById('proficiencyLevel').value;

        // Validation
        if (!username || !email || !password) {
            this.showAlert('Please fill in all required fields', 'error');
            return;
        }

        if (password.length < 8) {
            this.showAlert('Password must be at least 8 characters long', 'error');
            return;
        }

        if (!this.validateEmail(email)) {
            this.showAlert('Please enter a valid email address', 'error');
            return;
        }

        if (nativeLanguage === targetLanguage) {
            this.showAlert('Native language and target language should be different', 'warning');
        }

        this.showLoading('Creating your account...');

        try {
            const response = await fetch(`${API_BASE_URL}/api/auth/register`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify({
                    username,
                    email,
                    password,
                    nativeLanguage,
                    targetLanguage,
                    proficiencyLevel
                })
            });

            const data = await response.json();

            if (response.ok) {
                // Save token and user data
                localStorage.setItem('authToken', data.token);
                localStorage.setItem('userData', JSON.stringify(data.user));

                this.showAlert('Account created successfully! Redirecting...', 'success');
                
                setTimeout(() => {
                    window.location.href = '/index.html';
                }, 1500);
            } else {
                this.hideLoading();
                this.showAlert(data.message || data || 'Registration failed. Please try again.', 'error');
            }
        } catch (error) {
            this.hideLoading();
            console.error('Registration error:', error);
            this.showAlert('Connection error. Please try again.', 'error');
        }
    }

    validateEmail(email) {
        const re = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
        return re.test(email);
    }
}

// Initialize authentication manager when DOM is ready
document.addEventListener('DOMContentLoaded', () => {
    new AuthManager();
});
