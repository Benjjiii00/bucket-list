// src/router.ts
import { createRouter, createWebHistory } from 'vue-router';
import AuthService from './services/authService';

// Lazy-load components
const Home = () => import('./components/WorldMap.vue');
const Register = () => import('./components/Register.vue');
const Login = () => import('./components/Login.vue');
const PlaceDetail = () => import('./components/PlaceDetail.vue');

const routes = [
  {
    path: '/',
    name: 'Home',
    component: Home,
    meta: { requiresAuth: true }
  },
  {
    path: '/register',
    name: 'Register',
    component: Register,
    meta: { requiresAuth: false }
  },
  {
    path: '/login',
    name: 'Login',
    component: Login,
    meta: { requiresAuth: false }
  },
  {
    path: '/places/:id',
    name: 'PlaceDetail',
    component: PlaceDetail,
    meta: { requiresAuth: true }
  }
];

const router = createRouter({
  history: createWebHistory(),
  routes
});

// Route Guard für Authentifizierung
router.beforeEach((to) => {
  const isAuthenticated = AuthService.isAuthenticated();
  const requiresAuth = to.meta.requiresAuth;

  if (requiresAuth && !isAuthenticated) {
    return '/login';
  } else if (!requiresAuth && isAuthenticated && (to.path === '/login' || to.path === '/register')) {
    return '/';
  }
});

export default router;
