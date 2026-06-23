<template>
  <div class="auth-page">
    <div class="auth-card">
      <h1>Willkommen zurück</h1>
      <p>Melde dich an, um deine Bucket List zu verwalten.</p>

      <form @submit.prevent="handleLogin">
        <div class="auth-field">
          <label for="email">E-Mail</label>
          <input id="email" v-model="form.email" type="email" placeholder="deine@email.com" @blur="validateField('email')" />
          <span v-if="errors.email" class="field-error">{{ errors.email }}</span>
        </div>

        <div class="auth-field">
          <label for="password">Passwort</label>
          <input id="password" v-model="form.password" type="password" placeholder="Dein Passwort" @blur="validateField('password')" />
          <span v-if="errors.password" class="field-error">{{ errors.password }}</span>
        </div>

        <div v-if="generalError" class="auth-error">{{ generalError }}</div>

        <button type="submit" class="auth-btn" :disabled="isLoading">
          {{ isLoading ? 'Wird angemeldet …' : 'Anmelden' }}
        </button>
      </form>

      <p class="auth-link">
        Kein Konto? <router-link to="/register">Hier registrieren</router-link>
      </p>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive } from 'vue';
import { useRouter } from 'vue-router';
import authService from '../services/authService';

const router = useRouter();

const form = reactive({ email: '', password: '' });
const errors = reactive({ email: '', password: '' });
const generalError = ref('');
const isLoading = ref(false);

const validateField = (field: string) => {
  const value = form[field as keyof typeof form];
  if (field === 'email') {
    if (!value) errors.email = 'E-Mail ist erforderlich';
    else if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(value)) errors.email = 'Ungültiges E-Mail-Format';
    else errors.email = '';
  }
  if (field === 'password') {
    errors.password = !value ? 'Passwort ist erforderlich' : '';
  }
};

const handleLogin = async () => {
  validateField('email');
  validateField('password');
  if (errors.email || errors.password) return;

  isLoading.value = true;
  generalError.value = '';

  try {
    await authService.login({ email: form.email, password: form.password });
    router.push('/');
  } catch (error: any) {
    if (error.errors) Object.assign(errors, error.errors);
    else generalError.value = error.message || 'Anmeldung fehlgeschlagen';
  } finally {
    isLoading.value = false;
  }
};
</script>
