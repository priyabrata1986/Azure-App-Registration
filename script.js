document.addEventListener('DOMContentLoaded', function() {
    const form = document.getElementById('appRequestForm');
    const modal = document.getElementById('successModal');

    form.addEventListener('submit', function(e) {
        e.preventDefault();
        
        if (validateForm()) {
            // Simulate API call to ServiceNow backend
            submitRequest(getFormData());
        }
    });

    function validateForm() {
        // Basic validation is handled by HTML5 'required' attributes
        // Add custom validation here if needed
        const appName = document.getElementById('appName').value;
        const namingConventionRegex = /^[a-zA-Z0-9]+-[a-zA-Z0-9]+-[a-zA-Z0-9]+$/;
        
        if (!namingConventionRegex.test(appName)) {
            alert('Application Name must follow the format: [Dept]-[App]-[Env] (e.g., HR-Portal-Prod)');
            return false;
        }
        return true;
    }

    function getFormData() {
        const formData = new FormData(form);
        const data = {};
        formData.forEach((value, key) => {
            data[key] = value;
        });
        // Handle checkbox manually
        data.clientSecret = document.getElementById('clientSecret').checked;
        return data;
    }

    function submitRequest(data) {
        // Simulate network delay and backend processing
        const submitBtn = form.querySelector('button[type="submit"]');
        const originalText = submitBtn.innerText;
        submitBtn.innerText = 'Processing...';
        submitBtn.disabled = true;

        console.log("Submitting payload to Automation Engine:", data);

        setTimeout(() => {
            // Success scenario
            submitBtn.innerText = originalText;
            submitBtn.disabled = false;
            showModal();
            form.reset();
        }, 1500);
    }

    window.showModal = function() {
        modal.style.display = 'flex';
    }

    window.closeModal = function() {
        modal.style.display = 'none';
    }

    // Close modal when clicking outside
    window.onclick = function(event) {
        if (event.target == modal) {
            closeModal();
        }
    }
});