// PASTE SEMUA SCRIPT DARI <script> DI index.html ANDA KE SINI

// Ganti fungsi navigateTo lama menjadi:
function navigateTo(pageName) {
    window.location.href = pageName + ".html";
}

function handleLogin(e) {
    e.preventDefault();
    // Cukup sesuaikan logika login di bawah
    window.location.href = "dashboard.html";
}

function logout() {
    window.location.href = "login.html";
}
