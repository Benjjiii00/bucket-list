<template>
  <div class="auth-page">
    <div class="auth-card">
      <h1>Konto erstellen</h1>
      <p>Registriere dich für deine persönliche Bucket List.</p>

      <form @submit.prevent="handleRegister">
        <div class="auth-field">
          <label for="email">E-Mail</label>
          <input id="email" v-model="form.email" type="email" placeholder="deine@email.com" @blur="validateField('email')" />
          <span v-if="errors.email" class="field-error">{{ errors.email }}</span>
        </div>

        <div class="auth-field">
          <label for="password">Passwort</label>
          <input id="password" v-model="form.password" type="password" placeholder="Mindestens 8 Zeichen" @blur="validateField('password')" />
          <span v-if="errors.password" class="field-error">{{ errors.password }}</span>
        </div>

        <div class="auth-field">
          <label for="confirmPassword">Passwort wiederholen</label>
          <input id="confirmPassword" v-model="form.confirmPassword" type="password" placeholder="Passwort wiederholen" @blur="validateField('confirmPassword')" />
          <span v-if="errors.confirmPassword" class="field-error">{{ errors.confirmPassword }}</span>
        </div>

        <div v-if="generalError" class="auth-error">{{ generalError }}</div>

        <button type="submit" class="auth-btn" :disabled="isLoading">
          {{ isLoading ? 'Wird registriert …' : 'Registrieren' }}
        </button>
      </form>

      <p class="auth-link">
        Hast du bereits ein Konto? <router-link to="/login">Hier anmelden</router-link>
      </p>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive } from 'vue';
import { useRouter } from 'vue-router';
import authService from '../services/authService';

const router = useRouter();

const form = reactive({ email: '', password: '', confirmPassword: '' });
const errors = reactive({ email: '', password: '', confirmPassword: '' });
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
    if (!value) errors.password = 'Passwort ist erforderlich';
    else if (value.length < 8) errors.password = 'Passwort muss mindestens 8 Zeichen lang sein';
    else errors.password = '';
  }
  if (field === 'confirmPassword') {
    if (!value) errors.confirmPassword = 'Passwort-Bestätigung ist erforderlich';
    else if (value !== form.password) errors.confirmPassword = 'Passwörter stimmen nicht überein';
    else errors.confirmPassword = '';
  }
};

const handleRegister = async () => {
  validateField('email');
  validateField('password');
  validateField('confirmPassword');
  if (errors.email || errors.password || errors.confirmPassword) return;

  isLoading.value = true;
  generalError.value = '';

  try {
    await authService.register({
      email: form.email,
      password: form.password,
      confirmPassword: form.confirmPassword,
    });
    router.push('/');
  } catch (error: any) {
    if (error.errors) Object.assign(errors, error.errors);
    else generalError.value = error.message || 'Registrierung fehlgeschlagen';
  } finally {
    isLoading.value = false;
  }
};
</script>
