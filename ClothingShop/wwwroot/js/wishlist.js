// Wishlist functionality
class WishlistManager {
    constructor() {
        this.init();
    }

    init() {
        // Khởi tạo trạng thái wishlist cho tất cả các nút
        this.updateAllWishlistButtons();
        
        // Cập nhật số lượng wishlist
        this.updateWishlistCount();
    }

    async updateAllWishlistButtons() {
        const buttons = document.querySelectorAll('.wishlist-btn');
        buttons.forEach(async (btn) => {
            const productId = btn.dataset.productId;
            if (productId) {
                const inWishlist = await this.checkWishlist(productId);
                this.updateButtonState(btn, inWishlist);
            }
        });
    }

    async checkWishlist(productId) {
        try {
            const response = await fetch(`/Wishlist/Check?productId=${productId}`);
            const data = await response.json();
            return data.inWishlist;
        } catch (error) {
            console.error('Error checking wishlist:', error);
            return false;
        }
    }

    async toggleWishlist(productId, button) {
        try {
            console.log('Toggling wishlist for product:', productId);
            
            const response = await fetch('/Wishlist/Toggle', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                },
                body: JSON.stringify({ productId: parseInt(productId) })
            });

            const data = await response.json();
            console.log('Toggle response:', data);

            if (data.success) {
                // Cập nhật tất cả các nút có cùng productId
                document.querySelectorAll(`[data-product-id="${productId}"]`).forEach(btn => {
                    this.updateButtonState(btn, data.inWishlist);
                });
                
                this.updateWishlistCount();
                this.showToast(data.message);
            }

            return data;
        } catch (error) {
            console.error('Error toggling wishlist:', error);
            this.showToast('Có lỗi xảy ra', 'error');
            return { success: false };
        }
    }

    updateButtonState(button, inWishlist) {
        const icon = button.querySelector('i');
        
        if (inWishlist) {
            icon.classList.remove('far');
            icon.classList.add('fas', 'text-danger');
            button.classList.add('active');
            
            // Nếu là nút có text, cập nhật text
            if (button.classList.contains('wishlist-btn-text')) {
                const textNode = Array.from(button.childNodes).find(node => node.nodeType === Node.TEXT_NODE);
                if (textNode) {
                    textNode.textContent = 'ĐÃ YÊU THÍCH';
                }
            }
        } else {
            icon.classList.remove('fas', 'text-danger');
            icon.classList.add('far');
            button.classList.remove('active');
            
            // Nếu là nút có text, cập nhật text
            if (button.classList.contains('wishlist-btn-text')) {
                const textNode = Array.from(button.childNodes).find(node => node.nodeType === Node.TEXT_NODE);
                if (textNode) {
                    textNode.textContent = 'THÊM VÀO YÊU THÍCH';
                }
            }
        }
    }

    async updateWishlistCount() {
        try {
            const response = await fetch('/Wishlist/Count');
            const data = await response.json();
            
            // Cập nhật badge số lượng (nếu có)
            const badge = document.querySelector('.wishlist-count-badge');
            if (badge) {
                badge.textContent = data.count;
                badge.style.display = data.count > 0 ? 'inline-block' : 'none';
            }
        } catch (error) {
            console.error('Error updating wishlist count:', error);
        }
    }

    showToast(message, type = 'success') {
        // Xóa toast cũ nếu có
        const oldToast = document.querySelector('.wishlist-toast');
        if (oldToast) {
            oldToast.remove();
        }

        const toast = document.createElement('div');
        toast.className = `alert alert-${type === 'error' ? 'danger' : 'success'} position-fixed top-0 start-50 translate-middle-x mt-3 wishlist-toast`;
        toast.style.zIndex = '9999';
        toast.style.minWidth = '300px';
        toast.style.textAlign = 'center';
        toast.innerHTML = `
            <i class="fas fa-${type === 'error' ? 'exclamation-circle' : 'check-circle'}"></i>
            ${message}
        `;
        document.body.appendChild(toast);

        setTimeout(() => {
            toast.style.opacity = '0';
            toast.style.transition = 'opacity 0.3s ease';
            setTimeout(() => toast.remove(), 300);
        }, 2500);
    }
}

// Khởi tạo WishlistManager khi DOM đã load
document.addEventListener('DOMContentLoaded', function() {
    const wishlistManager = new WishlistManager();

    // Xử lý click vào nút wishlist
    document.addEventListener('click', async function(e) {
        const wishlistBtn = e.target.closest('.wishlist-btn, .wishlist-btn-text');
        if (wishlistBtn) {
            e.preventDefault();
            e.stopPropagation();
            
            const productId = wishlistBtn.dataset.productId;
            if (productId) {
                await wishlistManager.toggleWishlist(productId, wishlistBtn);
            }
        }
    });
});
