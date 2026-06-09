<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import authService from './services/authService'

const user = ref(authService.getUser())
const isAuthenticated = computed(() => authService.isAuthenticated())

onMounted(() => {
  // Restabilish token from localStorage if available
  const token = authService.getToken()
  if (token) {
    user.value = authService.getUser()
  }
})

const handleLogout = () => {
  authService.logout()
  user.value = null
  window.location.href = '/login'
}
</script>

<template>
  <div class="app-shell">
    <!-- Header mit Auth-Info -->
    <header v-if="isAuthenticated" class="app-header">
      <div class="header-content">
        <h1>🌍 Bucket List</h1>
        <div class="header-right">
          <span class="user-email">{{ user?.email }}</span>
          <button @click="handleLogout" class="logout-btn">Abmelden</button>
        </div>
      </div>
    </header>

    <!-- Main Content -->
    <main>
      <router-view />
    </main>
  </div>
</template>

<style scoped>
.app-shell {
  display: flex;
  flex-direction: column;
  height: 100vh;
}

.app-header {
  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
  color: white;
  padding: 1rem 0;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.1);
}

.header-content {
  max-width: 1200px;
  margin: 0 auto;
  padding: 0 1rem;
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.header-content h1 {
  margin: 0;
  font-size: 1.5rem;
}

.header-right {
  display: flex;
  align-items: center;
  gap: 1rem;
}

.user-email {
  font-size: 0.9rem;
  opacity: 0.9;
}

.logout-btn {
  background: rgba(255, 255, 255, 0.2);
  color: white;
  border: 1px solid rgba(255, 255, 255, 0.5);
  padding: 0.5rem 1rem;
  border-radius: 4px;
  cursor: pointer;
  transition: all 0.3s;
  font-weight: 500;
}

.logout-btn:hover {
  background: rgba(255, 255, 255, 0.3);
  border-color: rgba(255, 255, 255, 0.7);
}

main {
  flex: 1;
  overflow: auto;
}
</style>
