<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import authService from './services/authService'

const user = ref(authService.getUser())
const isAuthenticated = computed(() => authService.isAuthenticated())

onMounted(() => {
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
    <nav class="topnav">
      <div class="topnav__inner">
        <div class="topnav__brand">
          <span class="topnav__logo">&#x1F3D4;</span>
          <span class="topnav__title">Wilderness &amp; Waypoints</span>
        </div>
        <div class="topnav__links" v-if="isAuthenticated">
          <a href="/" class="topnav__link topnav__link--active">Map</a>
        </div>
        <div class="topnav__actions">
          <template v-if="!isAuthenticated">
            <button class="topnav__btn topnav__btn--primary" @click="$router.push('/login')">Login</button>
          </template>
          <template v-else>
            <div class="topnav__user">
              <span class="topnav__email">{{ user?.email }}</span>
              <button class="topnav__btn topnav__btn--ghost" @click="handleLogout">Abmelden</button>
            </div>
          </template>
        </div>
      </div>
    </nav>
    <main class="app-main">
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

.topnav {
  position: sticky;
  top: 0;
  z-index: 50;
  background: var(--color-surface);
  box-shadow: var(--shadow-sm);
  border-bottom: 1px solid var(--color-outline-variant);
}

.topnav__inner {
  display: flex;
  justify-content: space-between;
  align-items: center;
  max-width: 1440px;
  margin: 0 auto;
  padding: 12px var(--space-md);
  gap: var(--space-sm);
}

.topnav__brand {
  display: flex;
  align-items: center;
  gap: 10px;
}

.topnav__logo {
  font-size: 24px;
}

.topnav__title {
  font-family: var(--font-display);
  font-size: 20px;
  font-weight: 700;
  color: var(--color-primary);
  white-space: nowrap;
}

.topnav__links {
  display: flex;
  align-items: center;
  gap: var(--space-md);
}

.topnav__link {
  font-size: 15px;
  font-weight: 600;
  color: var(--color-on-surface-variant);
  text-decoration: none;
  padding-bottom: 4px;
  transition: color 0.18s;
}

.topnav__link:hover {
  color: var(--color-primary);
}

.topnav__link--active {
  color: var(--color-primary);
  border-bottom: 2px solid var(--color-secondary);
}

.topnav__actions {
  display: flex;
  align-items: center;
  gap: var(--space-sm);
}

.topnav__user {
  display: flex;
  align-items: center;
  gap: var(--space-sm);
}

.topnav__email {
  font-size: 13px;
  color: var(--color-on-surface-variant);
}

.topnav__btn {
  padding: 8px 18px;
  border-radius: var(--radius-md);
  font-weight: 700;
  font-size: 14px;
  cursor: pointer;
  transition: opacity 0.18s, transform 0.12s;
  border: none;
}

.topnav__btn--primary {
  background: var(--color-primary-container);
  color: var(--color-on-primary-container);
}

.topnav__btn--primary:hover {
  opacity: 0.9;
}

.topnav__btn--ghost {
  background: transparent;
  color: var(--color-on-surface-variant);
  border: 1px solid var(--color-outline-variant);
}

.topnav__btn--ghost:hover {
  border-color: var(--color-outline);
}

.topnav__btn:active {
  transform: scale(0.97);
}

.app-main {
  flex: 1;
  overflow: hidden;
}

@media (max-width: 640px) {
  .topnav__title {
    font-size: 16px;
  }
  .topnav__email {
    display: none;
  }
  .topnav__inner {
    padding: 10px var(--space-sm);
  }
}
</style>
