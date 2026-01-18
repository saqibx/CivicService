// API Base URL
const API_BASE = '/api/requests';

// State
let currentPage = 1;
let totalPages = 1;
let currentFilters = {
    status: '',
    category: '',
    sort: 'createdAt_desc'
};

// DOM Elements
const requestForm = document.getElementById('requestForm');
const formMessage = document.getElementById('formMessage');
const requestsList = document.getElementById('requestsList');
const pagination = document.getElementById('pagination');
const prevPageBtn = document.getElementById('prevPage');
const nextPageBtn = document.getElementById('nextPage');
const pageInfo = document.getElementById('pageInfo');
const applyFiltersBtn = document.getElementById('applyFilters');
const statusModal = document.getElementById('statusModal');
const modalRequestId = document.getElementById('modalRequestId');
const newStatusSelect = document.getElementById('newStatus');
const saveStatusBtn = document.getElementById('saveStatus');
const cancelStatusBtn = document.getElementById('cancelStatus');
const modalCloseBtn = document.querySelector('#statusModal .modal-close');

// Initialize
document.addEventListener('DOMContentLoaded', () => {
    loadRequests();
    setupEventListeners();
});

// Event Listeners
function setupEventListeners() {
    if (requestForm) {
        requestForm.addEventListener('submit', handleFormSubmit);
    }
    if (applyFiltersBtn) {
        applyFiltersBtn.addEventListener('click', applyFilters);
    }
    if (prevPageBtn) {
        prevPageBtn.addEventListener('click', () => changePage(-1));
    }
    if (nextPageBtn) {
        nextPageBtn.addEventListener('click', () => changePage(1));
    }
    if (saveStatusBtn) {
        saveStatusBtn.addEventListener('click', handleStatusUpdate);
    }
    if (cancelStatusBtn) {
        cancelStatusBtn.addEventListener('click', closeModal);
    }
    if (modalCloseBtn) {
        modalCloseBtn.addEventListener('click', closeModal);
    }
    if (statusModal) {
        statusModal.addEventListener('click', (e) => {
            if (e.target === statusModal) closeModal();
        });
    }
}

// Form Submission - uses authFetch to associate request with logged-in user
async function handleFormSubmit(e) {
    e.preventDefault();

    const lat = document.getElementById('latitude').value;
    const lng = document.getElementById('longitude').value;

    const formData = {
        category: document.getElementById('category').value,
        address: document.getElementById('address').value,
        description: document.getElementById('description').value,
        latitude: lat ? parseFloat(lat) : null,
        longitude: lng ? parseFloat(lng) : null
    };

    try {
        // For anonymous submissions, get reCAPTCHA token if configured
        const isAnonymous = !isLoggedIn();
        const siteKey = window.APP_CONFIG?.RECAPTCHA_SITE_KEY;

        if (isAnonymous && siteKey && window.grecaptcha) {
            try {
                formData.captchaToken = await grecaptcha.execute(siteKey, { action: 'submit_request' });
            } catch (captchaErr) {
                console.error('reCAPTCHA error:', captchaErr);
                showMessage('error', 'Security verification failed. Please refresh and try again.');
                return;
            }
        }

        // Use authFetch to include JWT token if logged in
        const response = await authFetch(API_BASE, {
            method: 'POST',
            body: JSON.stringify(formData)
        });

        if (response.ok) {
            const result = await response.json();
            showMessage('success', `Service request submitted successfully! ID: ${result.id.substring(0, 8)}...`);
            requestForm.reset();
            // Clear location status
            const locationStatus = document.getElementById('locationStatus');
            if (locationStatus) {
                locationStatus.style.display = 'none';
            }
            loadRequests();
        } else {
            const error = await response.json();
            let errorMsg = 'Failed to submit request.';
            if (error.error) {
                errorMsg = error.error;
            } else if (error.errors) {
                errorMsg = Object.values(error.errors).flat().join(' ');
            }
            showMessage('error', errorMsg);
        }
    } catch (err) {
        showMessage('error', 'Network error. Please try again.');
        console.error(err);
    }
}

// Show Message
function showMessage(type, text) {
    formMessage.className = `message ${type}`;
    formMessage.textContent = text;
    formMessage.classList.remove('hidden');

    setTimeout(() => {
        formMessage.classList.add('hidden');
    }, 5000);
}

// Load Requests
async function loadRequests() {
    if (!requestsList) return;

    requestsList.innerHTML = '<p class="loading">Loading requests...</p>';

    const params = new URLSearchParams({
        page: currentPage,
        pageSize: 10,
        sort: currentFilters.sort
    });

    if (currentFilters.status) params.append('status', currentFilters.status);
    if (currentFilters.category) params.append('category', currentFilters.category);

    try {
        // Use authFetch so server knows the user for HasUpvoted flag
        const response = await authFetch(`${API_BASE}?${params}`);
        const data = await response.json();

        totalPages = data.totalPages;
        renderRequests(data.items);
        updatePagination(data);
    } catch (err) {
        requestsList.innerHTML = '<p class="error">Failed to load requests.</p>';
        console.error(err);
    }
}

// Render Requests - only show Update Status button for Admin
function renderRequests(requests) {
    if (!requests || requests.length === 0) {
        requestsList.innerHTML = `
            <div class="empty-state">
                <h3>No service requests found</h3>
                <p>Be the first to submit a request or adjust your filters.</p>
            </div>
        `;
        return;
    }

    const canUpdateStatus = typeof isAdmin === 'function' && isAdmin();

    requestsList.innerHTML = requests.map(request => `
        <div class="request-card">
            <div class="request-header">
                <span class="request-category">${formatCategory(request.category)}</span>
                <span class="request-status status-${request.status.toLowerCase()}">${formatStatus(request.status)}</span>
            </div>
            <div class="request-address">${escapeHtml(request.address)}</div>
            <div class="request-description">${escapeHtml(request.description)}</div>
            <div class="request-meta">
                <div>
                    <span class="request-id">${request.id.substring(0, 8)}...</span>
                    <span> &bull; ${formatDate(request.createdAt)}</span>
                </div>
                <div class="request-actions">
                    <button class="upvote-btn ${request.hasUpvoted ? 'upvoted' : ''}"
                            onclick="handleUpvote('${request.id}', ${request.hasUpvoted})"
                            title="${request.hasUpvoted ? 'Remove your vote' : 'I\'m affected too'}">
                        <span>${request.hasUpvoted ? '✓' : '+'}</span>
                        <span>Affected</span>
                        <span class="upvote-count">${request.upvoteCount}</span>
                    </button>
                    ${canUpdateStatus ? `
                    <button class="btn btn-small" onclick="openStatusModal('${request.id}', '${request.status}')">
                        Update Status
                    </button>
                    ` : ''}
                </div>
            </div>
        </div>
    `).join('');
}

// Format Helpers
function formatCategory(category) {
    const map = {
        'Pothole': 'Pothole',
        'StreetLight': 'Street Light',
        'Graffiti': 'Graffiti',
        'IllegalDumping': 'Illegal Dumping',
        'SidewalkRepair': 'Sidewalk Repair',
        'TreeMaintenance': 'Tree Maintenance',
        'WaterLeak': 'Water Leak',
        'Other': 'Other'
    };
    return map[category] || category;
}

function formatStatus(status) {
    const map = {
        'Open': 'Open',
        'InProgress': 'In Progress',
        'Closed': 'Closed'
    };
    return map[status] || status;
}

function formatDate(dateString) {
    const date = new Date(dateString);
    return date.toLocaleDateString('en-US', {
        year: 'numeric',
        month: 'short',
        day: 'numeric',
        hour: '2-digit',
        minute: '2-digit'
    });
}

function escapeHtml(text) {
    const div = document.createElement('div');
    div.textContent = text;
    return div.innerHTML;
}

// Pagination
function updatePagination(data) {
    if (!pagination) return;

    if (data.totalPages <= 1) {
        pagination.classList.add('hidden');
        return;
    }

    pagination.classList.remove('hidden');
    pageInfo.textContent = `Page ${data.page} of ${data.totalPages} (${data.totalCount} total)`;
    prevPageBtn.disabled = data.page <= 1;
    nextPageBtn.disabled = data.page >= data.totalPages;
}

function changePage(delta) {
    currentPage += delta;
    loadRequests();
    document.getElementById('requests').scrollIntoView({ behavior: 'smooth' });
}

// Filters
function applyFilters() {
    currentFilters = {
        status: document.getElementById('filterStatus').value,
        category: document.getElementById('filterCategory').value,
        sort: document.getElementById('filterSort').value
    };
    currentPage = 1;
    loadRequests();
}

// Status Update Modal
function openStatusModal(requestId, currentStatus) {
    if (!statusModal) return;

    modalRequestId.textContent = requestId.substring(0, 8) + '...';
    modalRequestId.dataset.fullId = requestId;
    newStatusSelect.value = currentStatus;
    statusModal.classList.remove('hidden');
}

function closeModal() {
    if (statusModal) {
        statusModal.classList.add('hidden');
    }
}

// Status update requires Admin authentication
async function handleStatusUpdate() {
    const requestId = modalRequestId.dataset.fullId;
    const newStatus = newStatusSelect.value;

    try {
        // Use authFetch to include JWT token (required for Admin)
        const response = await authFetch(`${API_BASE}/${requestId}/status`, {
            method: 'PUT',
            body: JSON.stringify({ status: newStatus })
        });

        if (response.ok) {
            closeModal();
            loadRequests();
        } else if (response.status === 401) {
            alert('Please log in as Admin to update request status.');
            closeModal();
        } else if (response.status === 403) {
            alert('You do not have permission to update request status.');
            closeModal();
        } else {
            alert('Failed to update status. Please try again.');
        }
    } catch (err) {
        alert('Network error. Please try again.');
        console.error(err);
    }
}

// Handle upvote/remove upvote ("I'm affected too")
async function handleUpvote(requestId, hasUpvoted) {
    try {
        const method = hasUpvoted ? 'DELETE' : 'POST';
        const response = await authFetch(`${API_BASE}/${requestId}/upvote`, {
            method: method
        });

        if (response.ok) {
            loadRequests(); // Refresh to show updated count
        } else if (response.status === 409) {
            // Already upvoted, refresh to sync state
            loadRequests();
        } else {
            console.error('Failed to update upvote');
        }
    } catch (err) {
        console.error('Error updating upvote:', err);
    }
}
