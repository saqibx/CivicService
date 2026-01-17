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
const modalCloseBtn = document.querySelector('.modal-close');

// Initialize
document.addEventListener('DOMContentLoaded', () => {
    loadRequests();
    setupEventListeners();
});

// Event Listeners
function setupEventListeners() {
    requestForm.addEventListener('submit', handleFormSubmit);
    applyFiltersBtn.addEventListener('click', applyFilters);
    prevPageBtn.addEventListener('click', () => changePage(-1));
    nextPageBtn.addEventListener('click', () => changePage(1));
    saveStatusBtn.addEventListener('click', handleStatusUpdate);
    cancelStatusBtn.addEventListener('click', closeModal);
    modalCloseBtn.addEventListener('click', closeModal);
    statusModal.addEventListener('click', (e) => {
        if (e.target === statusModal) closeModal();
    });
}

// Form Submission
async function handleFormSubmit(e) {
    e.preventDefault();

    const formData = {
        category: document.getElementById('category').value,
        address: document.getElementById('address').value,
        description: document.getElementById('description').value
    };

    try {
        const response = await fetch(API_BASE, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify(formData)
        });

        if (response.ok) {
            const result = await response.json();
            showMessage('success', `Service request submitted successfully! ID: ${result.id.substring(0, 8)}...`);
            requestForm.reset();
            loadRequests();
        } else {
            const error = await response.json();
            let errorMsg = 'Failed to submit request.';
            if (error.errors) {
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
    requestsList.innerHTML = '<p class="loading">Loading requests...</p>';

    const params = new URLSearchParams({
        page: currentPage,
        pageSize: 10,
        sort: currentFilters.sort
    });

    if (currentFilters.status) params.append('status', currentFilters.status);
    if (currentFilters.category) params.append('category', currentFilters.category);

    try {
        const response = await fetch(`${API_BASE}?${params}`);
        const data = await response.json();

        totalPages = data.totalPages;
        renderRequests(data.items);
        updatePagination(data);
    } catch (err) {
        requestsList.innerHTML = '<p class="error">Failed to load requests.</p>';
        console.error(err);
    }
}

// Render Requests
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
                    <button class="btn btn-small" onclick="openStatusModal('${request.id}', '${request.status}')">
                        Update Status
                    </button>
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
    modalRequestId.textContent = requestId.substring(0, 8) + '...';
    modalRequestId.dataset.fullId = requestId;
    newStatusSelect.value = currentStatus;
    statusModal.classList.remove('hidden');
}

function closeModal() {
    statusModal.classList.add('hidden');
}

async function handleStatusUpdate() {
    const requestId = modalRequestId.dataset.fullId;
    const newStatus = newStatusSelect.value;

    try {
        const response = await fetch(`${API_BASE}/${requestId}/status`, {
            method: 'PUT',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({ status: newStatus })
        });

        if (response.ok) {
            closeModal();
            loadRequests();
        } else {
            alert('Failed to update status. Please try again.');
        }
    } catch (err) {
        alert('Network error. Please try again.');
        console.error(err);
    }
}
