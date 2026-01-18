// Authentication management
const AUTH_TOKEN_KEY = 'civicservice_token';
const AUTH_USER_KEY = 'civicservice_user';

// Get stored auth data
function getToken() {
    return localStorage.getItem(AUTH_TOKEN_KEY);
}

function getUser() {
    const userData = localStorage.getItem(AUTH_USER_KEY);
    return userData ? JSON.parse(userData) : null;
}

function isLoggedIn() {
    return !!getToken();
}

function hasRole(role) {
    const user = getUser();
    return user?.roles?.includes(role) ?? false;
}

function isStaffOrAdmin() {
    return hasRole('Staff') || hasRole('Admin');
}

function isAdmin() {
    return hasRole('Admin');
}

// Store auth data
function setAuth(token, user) {
    localStorage.setItem(AUTH_TOKEN_KEY, token);
    localStorage.setItem(AUTH_USER_KEY, JSON.stringify(user));
}

function clearAuth() {
    localStorage.removeItem(AUTH_TOKEN_KEY);
    localStorage.removeItem(AUTH_USER_KEY);
}

// API helper with auth
async function authFetch(url, options = {}) {
    const token = getToken();
    const headers = {
        'Content-Type': 'application/json',
        ...options.headers
    };

    if (token) {
        headers['Authorization'] = `Bearer ${token}`;
    }

    return fetch(url, { ...options, headers });
}

// Auth API calls
async function login(email, password) {
    const response = await fetch('/api/auth/login', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ email, password })
    });

    const data = await response.json();

    if (data.success) {
        setAuth(data.token, data.user);
    }

    return data;
}

async function register(email, password, firstName, lastName) {
    const response = await fetch('/api/auth/register', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ email, password, firstName, lastName })
    });

    const data = await response.json();

    if (data.success) {
        setAuth(data.token, data.user);
    }

    return data;
}

function logout() {
    clearAuth();
    window.location.href = '/';
}

// Update UI based on auth state
function updateAuthUI() {
    const user = getUser();
    const authNav = document.getElementById('authNav');
    const userInfo = document.getElementById('userInfo');

    if (!authNav) return;

    if (isLoggedIn() && user) {
        authNav.innerHTML = `
            <a href="/my-requests.html" class="nav-link">My Requests</a>
            <span class="nav-user">${user.firstName}</span>
            <button onclick="logout()" class="btn btn-small btn-secondary">Logout</button>
        `;
    } else {
        authNav.innerHTML = `
            <button onclick="showLoginModal()" class="btn btn-small">Login</button>
            <button onclick="showRegisterModal()" class="btn btn-small btn-secondary">Register</button>
        `;
    }
}

// Modal functions
function showLoginModal() {
    document.getElementById('loginModal').classList.remove('hidden');
}

function hideLoginModal() {
    document.getElementById('loginModal').classList.add('hidden');
    document.getElementById('loginForm').reset();
    document.getElementById('loginError').classList.add('hidden');
}

function showRegisterModal() {
    document.getElementById('registerModal').classList.remove('hidden');
}

function hideRegisterModal() {
    document.getElementById('registerModal').classList.add('hidden');
    document.getElementById('registerForm').reset();
    document.getElementById('registerError').classList.add('hidden');
}

// Form handlers
async function handleLogin(e) {
    e.preventDefault();
    const form = e.target;
    const email = form.loginEmail.value;
    const password = form.loginPassword.value;
    const errorDiv = document.getElementById('loginError');
    const submitBtn = form.querySelector('button[type="submit"]');

    submitBtn.disabled = true;
    submitBtn.textContent = 'Logging in...';

    try {
        const result = await login(email, password);

        if (result.success) {
            hideLoginModal();
            updateAuthUI();
            // Always reload to refresh page state after login
            window.location.reload();
        } else {
            errorDiv.textContent = result.error || 'Login failed';
            errorDiv.classList.remove('hidden');
        }
    } catch (err) {
        errorDiv.textContent = 'Network error. Please try again.';
        errorDiv.classList.remove('hidden');
    } finally {
        submitBtn.disabled = false;
        submitBtn.textContent = 'Login';
    }
}

async function handleRegister(e) {
    e.preventDefault();
    const form = e.target;
    const email = form.registerEmail.value;
    const password = form.registerPassword.value;
    const firstName = form.registerFirstName.value;
    const lastName = form.registerLastName.value;
    const errorDiv = document.getElementById('registerError');
    const submitBtn = form.querySelector('button[type="submit"]');

    submitBtn.disabled = true;
    submitBtn.textContent = 'Creating account...';

    try {
        const result = await register(email, password, firstName, lastName);

        if (result.success) {
            hideRegisterModal();
            updateAuthUI();
            // Reload to refresh page state
            window.location.reload();
        } else {
            errorDiv.textContent = result.error || 'Registration failed';
            errorDiv.classList.remove('hidden');
        }
    } catch (err) {
        errorDiv.textContent = 'Network error. Please try again.';
        errorDiv.classList.remove('hidden');
    } finally {
        submitBtn.disabled = false;
        submitBtn.textContent = 'Create Account';
    }
}

// Initialize auth on page load
document.addEventListener('DOMContentLoaded', () => {
    updateAuthUI();

    // Set up modal event listeners
    const loginForm = document.getElementById('loginForm');
    const registerForm = document.getElementById('registerForm');

    if (loginForm) {
        loginForm.addEventListener('submit', handleLogin);
    }

    if (registerForm) {
        registerForm.addEventListener('submit', handleRegister);
    }

    // Close modals on backdrop click
    document.querySelectorAll('.modal').forEach(modal => {
        modal.addEventListener('click', (e) => {
            if (e.target === modal) {
                modal.classList.add('hidden');
            }
        });
    });
});
