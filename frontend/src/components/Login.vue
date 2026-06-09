<!-- src/components/Login.vue -->
<template>
  <div class="auth-container">
    <div class="auth-form">
      <h2>Anmeldung</h2>
      
      <form @submit.prevent="handleLogin">
        <!-- Email Feld -->
        <div class="form-group">
          <label for="email">E-Mail:</label>
          <input
            id="email"
            v-model="form.email"
            type="email"
            placeholder="deine@email.com"
            @blur="validateField('email')"
          />
          <span v-if="errors.email" class="error-message">{{ errors.email }}</span>
        </div>

        <!-- Passwort Feld -->
        <div class="form-group">
          <label for="password">Passwort:</label>
          <input
            id="password"
            v-model="form.password"
            type="password"
            placeholder="Dein Passwort"
            @blur="validateField('password')"
          />
          <span v-if="errors.password" class="error-message">{{ errors.password }}</span>
        </div>

        <!-- Fehler Nachricht -->
        <div v-if="generalError" class="error-alert">{{ generalError }}</div>

        <!-- Submit Button -->
        <button type="submit" :disabled="isLoading">
          {{ isLoading ? 'Wird angemeldet...' : 'Anmelden' }}
        </button>
      </form>

      <!-- Link zur Registrierung -->
      <p class="form-footer">
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

const form = reactive({
  email: '',
  password: ''
});

const errors = reactive({
  email: '',
  password: ''
});

const generalError = ref('');
const isLoading = ref(false);

const validateField = (field: string) => {
  const value = form[field as keyof typeof form];
  
  if (field === 'email') {
    if (!value) {
      errors.email = 'E-Mail ist erforderlich';
    } else if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(value)) {
      errors.email = 'Ungültiges E-Mail-Format';
    } else {
      errors.email = '';
    }
  }
  
  if (field === 'password') {
    if (!value) {
      errors.password = 'Passwort ist erforderlich';
    } else {
      errors.password = '';
    }
  }
};

const handleLogin = async () => {
  // Alle Felder validieren
  validateField('email');
  validateField('password');

  if (errors.email || errors.password) {
    return;
  }

  isLoading.value = true;
  generalError.value = '';

  try {
    await authService.login({
      email: form.email,
      password: form.password
    });

    router.push('/');
  } catch (error: any) {
    if (error.errors) {
      Object.assign(errors, error.errors);
    } else {
      generalError.value = error.message || 'Anmeldung fehlgeschlagen';
    }
  } finally {
    isLoading.value = false;
  }
};
</script>

<style scoped>
.auth-container {
  display: flex;
  justify-content: center;
  align-items: center;
  min-height: 100vh;
  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
}

.auth-form {
  background: white;
  padding: 2rem;
  border-radius: 8px;
  box-shadow: 0 10px 25px rgba(0, 0, 0, 0.2);
  width: 100%;
  max-width: 400px;
}

h2 {
  text-align: center;
  margin-bottom: 1.5rem;
  color: #333;
}

.form-group {
  margin-bottom: 1rem;
}

label {
  display: block;
  margin-bottom: 0.5rem;
  color: #555;
  font-weight: 500;
}

input {
  width: 100%;
  padding: 0.75rem;
  border: 1px solid #ddd;
  border-radius: 4px;
  font-size: 1rem;
  transition: border-color 0.3s;
  box-sizing: border-box;
}

input:focus {
  outline: none;
  border-color: #667eea;
  box-shadow: 0 0 0 3px rgba(102, 126, 234, 0.1);
}

.error-message {
  color: #e74c3c;
  font-size: 0.875rem;
  margin-top: 0.25rem;
  display: block;
}

.error-alert {
  background-color: #ffe6e6;
  border: 1px solid #e74c3c;
  color: #c0392b;
  padding: 0.75rem;
  border-radius: 4px;
  margin-bottom: 1rem;
}

button {
  width: 100%;
  padding: 0.75rem;
  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
  color: white;
  border: none;
  border-radius: 4px;
  font-size: 1rem;
  font-weight: 600;
  cursor: pointer;
  transition: opacity 0.3s;
}

button:hover:not(:disabled) {
  opacity: 0.9;
}

button:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

.form-footer {
  text-align: center;
  margin-top: 1rem;
  color: #666;
}

.form-footer a {
  color: #667eea;
  text-decoration: none;
  font-weight: 600;
}

.form-footer a:hover {
  text-decoration: underline;
}
</style>
