// Cart functionality - Add to cart with offcanvas

// Function to add product to cart via AJAX and show offcanvas
function addToCartWithCanvas(productId, quantity, size, color) {
    const formData = new FormData();
    formData.append('productId', productId);
    formData.append('quantity', quantity || 1);
    if (size) formData.append('selectedSize', size);
    if (color) formData.append('selectedColor', color);

    fetch('/Cart/Add', {
        method: 'POST',
        headers: {
            'X-Requested-With': 'XMLHttpRequest'
        },
        body: formData
    })
    .then(response => response.json())
    .then(data => {
        if (data.success) {
            // Update cart badge
            updateCartBadge(data.cartCount);
            
            // Show success toast
            showToast('success', data.message);
            
            // Open cart offcanvas
            const cartOffcanvas = new bootstrap.Offcanvas(document.getElementById('cartOffcanvas'));
            cartOffcanvas.show();
        } else {
            showToast('error', data.message || 'Có lỗi xảy ra!');
        }
    })
    .catch(error => {
        console.error('Error:', error);
        showToast('error', 'Không thể thêm vào giỏ hàng!');
    });
}

// Update cart badge count
function updateCartBadge(count) {
    const cartButton = document.querySelector('[data-bs-target="#cartOffcanvas"]');
    if (!cartButton) return;
    
    let badge = cartButton.querySelector('.badge.bg-danger');
    
    if (count > 0) {
        if (!badge) {
            badge = document.createElement('span');
            badge.className = 'badge bg-danger rounded-pill position-absolute top-0 start-100 translate-middle';
            badge.style.fontSize = '0.65rem';
            cartButton.appendChild(badge);
        }
        badge.textContent = count;
    } else {
        if (badge) {
            badge.remove();
        }
    }
}

// Show toast notification
function showToast(type, message) {
    // Remove existing toasts
    const existingToast = document.querySelector('.custom-toast');
    if (existingToast) {
        existingToast.remove();
    }
    
    const toast = document.createElement('div');
    toast.className = `custom-toast alert alert-${type === 'success' ? 'success' : 'danger'} position-fixed`;
    toast.style.cssText = 'top: 80px; right: 20px; z-index: 9999; min-width: 300px; box-shadow: 0 4px 12px rgba(0,0,0,0.15);';
    toast.innerHTML = `
        <div class="d-flex align-items-center">
            <i class="fas fa-${type === 'success' ? 'check-circle' : 'exclamation-circle'} me-2"></i>
            <span>${message}</span>
        </div>
    `;
    
    document.body.appendChild(toast);
    
    // Auto remove after 3 seconds
    setTimeout(() => {
        toast.style.transition = 'opacity 0.3s';
        toast.style.opacity = '0';
        setTimeout(() => toast.remove(), 300);
    }, 3000);
}

// Handle add to cart button clicks
document.addEventListener('DOMContentLoaded', function() {
    // Handle all "Add to Cart" buttons
    document.addEventListener('click', function(e) {
        const addToCartBtn = e.target.closest('.btn-add-to-cart');
        if (addToCartBtn) {
            e.preventDefault();
            
            const productId = addToCartBtn.dataset.productId;
            const quantity = addToCartBtn.dataset.quantity || 1;
            const size = addToCartBtn.dataset.size || null;
            const color = addToCartBtn.dataset.color || null;
            
            addToCartWithCanvas(productId, quantity, size, color);
        }
    });
});
