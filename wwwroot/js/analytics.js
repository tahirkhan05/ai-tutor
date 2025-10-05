// Analytics Manager
class AnalyticsManager {
    constructor() {
        this.API_BASE = 'http://localhost:5000/api';
        this.token = localStorage.getItem('authToken');
        this.userData = JSON.parse(localStorage.getItem('userData') || '{}');
        this.charts = {
            progress: null,
            skills: null
        };
        
        this.init();
    }
    
    async init() {
        // Check authentication
        if (!this.token) {
            window.location.href = '/login.html';
            return;
        }
        
        // Verify token
        const isValid = await this.verifyToken();
        if (!isValid) {
            window.location.href = '/login.html';
            return;
        }
        
        // Setup UI
        this.setupEventListeners();
        this.updateUserInfo();
        
        // Load analytics data
        await this.loadDashboard();
    }
    
    async verifyToken() {
        try {
            const response = await fetch(`${this.API_BASE}/auth/me`, {
                headers: { 'Authorization': `Bearer ${this.token}` }
            });
            
            if (response.ok) {
                const data = await response.json();
                this.userData = data;
                localStorage.setItem('userData', JSON.stringify(data));
                return true;
            }
            return false;
        } catch (error) {
            console.error('Token verification failed:', error);
            return false;
        }
    }
    
    setupEventListeners() {
        // Back to app button
        document.getElementById('backToAppBtn').addEventListener('click', () => {
            window.location.href = '/index.html';
        });
        
        // Logout button
        document.getElementById('logoutBtn').addEventListener('click', () => {
            localStorage.removeItem('authToken');
            localStorage.removeItem('userData');
            window.location.href = '/login.html';
        });
        
        // Theme toggle
        document.getElementById('themeToggle').addEventListener('click', () => {
            const html = document.documentElement;
            const currentTheme = html.getAttribute('data-theme');
            const newTheme = currentTheme === 'dark' ? 'light' : 'dark';
            html.setAttribute('data-theme', newTheme);
            localStorage.setItem('theme', newTheme);
            
            // Update charts
            if (this.charts.progress) {
                this.updateChartTheme(this.charts.progress);
            }
            if (this.charts.skills) {
                this.updateChartTheme(this.charts.skills);
            }
        });
        
        // Progress period selector
        document.getElementById('progressPeriod').addEventListener('change', async (e) => {
            await this.loadProgressChart(parseInt(e.target.value));
        });
        
        // Load saved theme
        const savedTheme = localStorage.getItem('theme') || 'dark';
        document.documentElement.setAttribute('data-theme', savedTheme);
    }
    
    updateUserInfo() {
        if (this.userData.username) {
            document.getElementById('userName').textContent = this.userData.username;
        }
        if (this.userData.profile) {
            document.getElementById('userLevel').textContent = this.userData.profile.proficiencyLevel || 'Beginner';
            document.getElementById('userLanguage').textContent = 
                `Learning ${this.userData.profile.targetLanguage || 'English'}`;
        }
    }
    
    async loadDashboard() {
        this.showLoading(true);
        
        try {
            console.log('📊 Loading dashboard data...');
            const response = await fetch(`${this.API_BASE}/analytics/dashboard`, {
                headers: { 'Authorization': `Bearer ${this.token}` }
            });
            
            console.log('📡 Dashboard response status:', response.status);
            
            if (response.ok) {
                const data = await response.json();
                console.log('📦 Dashboard data received:', data);
                this.updateStats(data);
                await this.loadProgressChart(30);
                await this.loadSkillsChart();
                await this.loadWeakAreas();
                await this.loadSessionHistory();
            } else {
                const errorText = await response.text();
                console.error('❌ Dashboard load failed:', response.status, errorText);
                this.showError('Failed to load dashboard data: ' + response.status);
            }
        } catch (error) {
            console.error('❌ Dashboard load error:', error);
            this.showError('Failed to connect to server: ' + error.message);
        } finally {
            this.showLoading(false);
        }
    }
    
    updateStats(data) {
        // Handle PascalCase from backend
        const overview = data.Overview || data.overview || data;
        
        console.log('📊 Updating stats with overview:', overview);
        
        // Total minutes
        const totalMinutes = Math.round(overview.TotalMinutes || overview.totalMinutes || 0);
        document.getElementById('totalMinutes').textContent = totalMinutes;
        console.log('⏱️ Total Minutes:', totalMinutes);
        
        // Total sessions
        const totalSessions = overview.TotalSessions || overview.totalSessions || 0;
        document.getElementById('totalSessions').textContent = totalSessions;
        console.log('📝 Total Sessions:', totalSessions);
        
        // Average accuracy
        const accuracy = overview.AverageAccuracy || overview.averageAccuracy || 0;
        document.getElementById('avgAccuracy').textContent = `${Math.round(accuracy)}%`;
        console.log('✅ Avg Accuracy:', accuracy);
        
        // Current streak
        const streak = overview.CurrentStreak || overview.currentStreak || 0;
        document.getElementById('currentStreak').textContent = streak;
        console.log('🔥 Current Streak:', streak);
        
        // Update skill scores if available
        const weeklyProgress = data.WeeklyProgress || data.weeklyProgress;
        console.log('📈 Weekly Progress:', weeklyProgress);
        
        if (weeklyProgress && weeklyProgress.length > 0) {
            const latest = weeklyProgress[weeklyProgress.length - 1];
            this.updateSkillBars({
                grammar: latest.GrammarScore || latest.grammarScore || 0,
                vocabulary: latest.VocabularyScore || latest.vocabularyScore || 0,
                pronunciation: latest.PronunciationScore || latest.pronunciationScore || 0,
                fluency: latest.FluencyScore || latest.fluencyScore || 0
            });
        } else {
            console.warn('⚠️ No weekly progress data available');
        }
    }
    
    updateSkillBars(scores) {
        const skills = ['grammar', 'vocabulary', 'pronunciation', 'fluency'];
        
        skills.forEach(skill => {
            const score = scores[skill] || 0;
            const scoreElement = document.getElementById(`${skill}Score`);
            const barElement = document.getElementById(`${skill}Bar`);
            
            if (scoreElement && barElement) {
                scoreElement.textContent = `${Math.round(score)}%`;
                setTimeout(() => {
                    barElement.style.width = `${score}%`;
                }, 100);
            }
        });
    }
    
    async loadProgressChart(days = 30) {
        try {
            const response = await fetch(
                `${this.API_BASE}/analytics/progress?days=${days}`,
                { headers: { 'Authorization': `Bearer ${this.token}` } }
            );
            
            if (response.ok) {
                const data = await response.json();
                this.renderProgressChart(data);
            }
        } catch (error) {
            console.error('Progress chart error:', error);
        }
    }
    
    renderProgressChart(data) {
        const ctx = document.getElementById('progressChart');
        
        // Destroy existing chart
        if (this.charts.progress) {
            this.charts.progress.destroy();
        }
        
        // Parse data array from backend
        const progressData = Array.isArray(data) ? data : [];
        const labels = progressData.map(d => {
            const date = new Date(d.Date || d.date);
            return date.toLocaleDateString('en-US', { month: 'short', day: 'numeric' });
        });
        const accuracyData = progressData.map(d => d.AverageAccuracy || d.averageAccuracy || 0);
        const minutesData = progressData.map(d => d.MinutesLearned || d.minutesLearned || 0);
        
        const isDark = document.documentElement.getAttribute('data-theme') === 'dark';
        const textColor = isDark ? '#e2e8f0' : '#334155';
        const gridColor = isDark ? '#334155' : '#e2e8f0';
        
        this.charts.progress = new Chart(ctx, {
            type: 'line',
            data: {
                labels: labels,
                datasets: [
                    {
                        label: 'Accuracy %',
                        data: accuracyData,
                        borderColor: '#667eea',
                        backgroundColor: 'rgba(102, 126, 234, 0.1)',
                        tension: 0.4,
                        fill: true
                    },
                    {
                        label: 'Minutes',
                        data: minutesData,
                        borderColor: '#f093fb',
                        backgroundColor: 'rgba(240, 147, 251, 0.1)',
                        tension: 0.4,
                        fill: true
                    }
                ]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                plugins: {
                    legend: {
                        labels: { color: textColor }
                    },
                    tooltip: {
                        mode: 'index',
                        intersect: false
                    }
                },
                scales: {
                    x: {
                        grid: { color: gridColor },
                        ticks: { color: textColor }
                    },
                    y: {
                        grid: { color: gridColor },
                        ticks: { color: textColor }
                    }
                }
            }
        });
    }
    
    async loadSkillsChart() {
        try {
            const response = await fetch(
                `${this.API_BASE}/analytics/dashboard`,
                { headers: { 'Authorization': `Bearer ${this.token}` } }
            );
            
            if (response.ok) {
                const data = await response.json();
                const weeklyProgress = data.WeeklyProgress || data.weeklyProgress;
                if (weeklyProgress && weeklyProgress.length > 0) {
                    const latest = weeklyProgress[weeklyProgress.length - 1];
                    this.renderSkillsChart({
                        grammar: latest.GrammarScore || latest.grammarScore || 0,
                        vocabulary: latest.VocabularyScore || latest.vocabularyScore || 0,
                        pronunciation: latest.PronunciationScore || latest.pronunciationScore || 0,
                        fluency: latest.FluencyScore || latest.fluencyScore || 0
                    });
                }
            }
        } catch (error) {
            console.error('Skills chart error:', error);
        }
    }
    
    renderSkillsChart(scores) {
        const ctx = document.getElementById('skillsChart');
        
        // Destroy existing chart
        if (this.charts.skills) {
            this.charts.skills.destroy();
        }
        
        const isDark = document.documentElement.getAttribute('data-theme') === 'dark';
        const textColor = isDark ? '#e2e8f0' : '#334155';
        
        this.charts.skills = new Chart(ctx, {
            type: 'doughnut',
            data: {
                labels: ['Grammar', 'Vocabulary', 'Pronunciation', 'Fluency'],
                datasets: [{
                    data: [
                        scores.grammar || 0,
                        scores.vocabulary || 0,
                        scores.pronunciation || 0,
                        scores.fluency || 0
                    ],
                    backgroundColor: [
                        '#667eea',
                        '#f093fb',
                        '#4facfe',
                        '#fa709a'
                    ],
                    borderWidth: 0
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                plugins: {
                    legend: {
                        position: 'bottom',
                        labels: { 
                            color: textColor,
                            padding: 15
                        }
                    }
                }
            }
        });
    }
    
    async loadWeakAreas() {
        try {
            const response = await fetch(
                `${this.API_BASE}/analytics/weak-areas`,
                { headers: { 'Authorization': `Bearer ${this.token}` } }
            );
            
            if (response.ok) {
                const data = await response.json();
                this.renderWeakAreas(data);
            }
        } catch (error) {
            console.error('Weak areas error:', error);
        }
    }
    
    renderWeakAreas(areas) {
        const container = document.getElementById('weakAreasList');
        
        if (!areas || areas.length === 0) {
            container.innerHTML = `
                <div class="empty-state">
                    <i data-feather="check-circle"></i>
                    <p>No weak areas identified yet. Keep practicing!</p>
                </div>
            `;
            feather.replace();
            return;
        }
        
        container.innerHTML = areas.map(area => `
            <div class="weak-area-item">
                <div class="weak-area-header">
                    <span class="weak-area-type">${area.ErrorType || area.errorType}</span>
                    <span class="weak-area-count">${area.Count || area.count} errors</span>
                </div>
                <div class="weak-area-examples">
                    ${(area.Examples || area.examples || []).map(ex => 
                        `<strong>"${ex.OriginalText || ex.originalText}"</strong> → "${ex.CorrectedText || ex.correctedText}"`
                    ).join(', ')}
                </div>
            </div>
        `).join('');
        feather.replace();
    }
    
    async loadSessionHistory() {
        try {
            const response = await fetch(
                `${this.API_BASE}/session/history`,
                { headers: { 'Authorization': `Bearer ${this.token}` } }
            );
            
            if (response.ok) {
                const data = await response.json();
                this.renderSessionHistory(data);
            }
        } catch (error) {
            console.error('Session history error:', error);
        }
    }
    
    renderSessionHistory(sessions) {
        const container = document.getElementById('sessionHistory');
        
        if (!sessions || sessions.length === 0) {
            container.innerHTML = `
                <div class="empty-state">
                    <i data-feather="calendar"></i>
                    <p>No sessions yet. Start your first conversation!</p>
                </div>
            `;
            feather.replace();
            return;
        }
        
        // Show last 10 sessions
        const recentSessions = sessions.slice(0, 10);
        
        container.innerHTML = recentSessions.map(session => {
            const startTime = session.StartTime || session.startTime;
            const date = new Date(startTime);
            const dateStr = date.toLocaleDateString('en-US', { 
                month: 'short', 
                day: 'numeric',
                year: 'numeric'
            });
            const timeStr = date.toLocaleTimeString('en-US', {
                hour: '2-digit',
                minute: '2-digit'
            });
            
            const duration = Math.round(session.DurationMinutes || session.durationMinutes || 0);
            const accuracy = Math.round(session.AccuracyScore || session.accuracyScore || 0);
            const messageCount = session.TotalMessages || session.totalMessages || session.MessageCount || session.messageCount || 0;
            const correctionCount = session.CorrectionsGiven || session.correctionsGiven || session.CorrectionCount || session.correctionCount || 0;
            
            return `
                <div class="session-item">
                    <div class="session-header">
                        <span class="session-date">${dateStr} at ${timeStr}</span>
                        <span class="session-duration">${duration} min</span>
                    </div>
                    <div class="session-stats">
                        <span class="session-stat">
                            <i data-feather="message-circle"></i>
                            ${messageCount} messages
                        </span>
                        <span class="session-stat">
                            <i data-feather="alert-circle"></i>
                            ${correctionCount} corrections
                        </span>
                        <span class="session-stat accuracy">
                            <i data-feather="check-circle"></i>
                            ${accuracy}% accuracy
                        </span>
                    </div>
                </div>
            `;
        }).join('');
        
        feather.replace();
    }
    
    updateChartTheme(chart) {
        const isDark = document.documentElement.getAttribute('data-theme') === 'dark';
        const textColor = isDark ? '#e2e8f0' : '#334155';
        const gridColor = isDark ? '#334155' : '#e2e8f0';
        
        if (chart.options.plugins && chart.options.plugins.legend) {
            chart.options.plugins.legend.labels.color = textColor;
        }
        
        if (chart.options.scales) {
            if (chart.options.scales.x) {
                chart.options.scales.x.grid.color = gridColor;
                chart.options.scales.x.ticks.color = textColor;
            }
            if (chart.options.scales.y) {
                chart.options.scales.y.grid.color = gridColor;
                chart.options.scales.y.ticks.color = textColor;
            }
        }
        
        chart.update();
    }
    
    showLoading(show) {
        const overlay = document.getElementById('loadingOverlay');
        if (show) {
            overlay.classList.add('active');
        } else {
            overlay.classList.remove('active');
        }
    }
    
    showError(message) {
        alert(message); // You can replace with a better UI notification
    }
}

// Initialize when DOM is ready
document.addEventListener('DOMContentLoaded', () => {
    new AnalyticsManager();
});
