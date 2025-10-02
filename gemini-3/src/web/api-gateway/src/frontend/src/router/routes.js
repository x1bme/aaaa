const routes = [
  {
    path: '/',
    component: () => import('layouts/MainLayout.vue'),
    children: [
      {path: '', component: () => import('pages/Home.vue'), meta: { requiresAuth: true }},
      {path: 'login', component: () => import('pages/LoginPage.vue')},
      {path: 'logout', component: () => import('pages/LogoutPage.vue')},
      {path: 'Callback', component: () => import('pages/Callback.vue')},
      {path: 'ValveLogs', component: () => import('pages/ValveLogs.vue'), meta: { requiresAuth: true }},
      {path: 'ProfileSettings', component: () => import('pages/ProfileSettings.vue'), meta: { requiresAuth: true }},
      {path: 'Valves', component: () => import('pages/ValveDisplay.vue'), meta: { requiresAuth: true }},
      {path: 'Daus', component: () => import('pages/DauDisplay.vue'), meta: { requiresAuth: true }},

      // Not completed yet
      // {path: '/Taskboard', component: () => import('pages/TaskBoard.vue')},
    ]
  },

  // Always leave this as last one,
  // but you can also remove it
  {
    path: '/:catchAll(.*)*',
    component: () => import('pages/Error404.vue')
  }
]

export default routes
