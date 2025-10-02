<template>
  <q-layout view="lHh Lpr lFf">
    <q-header elevated>
      <q-toolbar>
        <q-btn
          flat
          dense
          round
          @click="toggleLeftDrawer"
          icon="menu"
          aria-label="Menu"
        />
        <q-space/>
        <div class="q-gutter-sm row items-center no-wrap">
                    <q-btn round dense flat color="white" :icon="quasar.fullscreen.isActive ? 'fullscreen_exit' : 'fullscreen'"
                           @click="quasar.fullscreen.toggle()"
                           v-if="quasar.screen.gt.sm">
                    </q-btn>
        </div>
      </q-toolbar>
    </q-header>

    <q-drawer
      v-model="leftDrawerOpen"
      show-if-above
      bordered
      class="bg-primary text-white"
    >
      <q-list>
        <q-item to="/" exact active-class="menu-item-active">
          <q-item-section avatar>
            <q-icon name="home"/>
          </q-item-section>
          <q-item-section>
            <q-item-label>Project Gemini</q-item-label>
          </q-item-section>
        </q-item>

        <q-expansion-item
          icon="list"
          label="Valve Management"
        >
          <q-item to="/Valves" class="q-ml-xl" active-class="menu-item-active">
          <q-item-section avatar>
            <q-icon name="water"/>
          </q-item-section>
            <q-item-section>
              <q-item-label>Valves</q-item-label>
            </q-item-section>
          </q-item>

          <q-item to="/Daus" class="q-ml-xl" active-class="menu-item-active">
          <q-item-section avatar>
            <q-icon name="router"/>
          </q-item-section>
            <q-item-section>
              <q-item-label>Daus</q-item-label>
            </q-item-section>
          </q-item>

          <q-item to="/ValveLogs" class="q-ml-xl" active-class="menu-item-active">
          <q-item-section avatar>
            <q-icon name="article"/>
          </q-item-section>
            <q-item-section>
              <q-item-label>Valve Logs</q-item-label>
            </q-item-section>
          </q-item>
        </q-expansion-item>

        <!-- Use /Profile in future -->
        <q-item to="/ProfileSettings" active-class="menu-item-active">
          <q-item-section avatar>
            <q-icon name="manage_accounts"/>
          </q-item-section>
          <q-item-section>
            <q-item-label>User Settings</q-item-label>
          </q-item-section>
        </q-item>

        <q-item to="/logout" active-class="menu-item-active">
          <q-item-section avatar>
            <q-icon name="logout"/>
          </q-item-section>
          <q-item-section>
            <q-item-label>Logout</q-item-label>
          </q-item-section>
        </q-item>

      </q-list>
    </q-drawer>
    <q-page-container class="bg-grey-2">
      <router-view/>
    </q-page-container>
  </q-layout>
</template>

<script>

import {defineComponent, ref} from 'vue'
import {useQuasar} from "quasar";

export default defineComponent({
  name: 'MainLayout',


  setup() {
    const leftDrawerOpen = ref(false)
    const quasar = useQuasar()

    return {
      quasar,
      leftDrawerOpen,
      toggleLeftDrawer() {
        leftDrawerOpen.value = !leftDrawerOpen.value
      }
    }
  }
})
</script>

<style>

/* FONT AWESOME GENERIC BEAT */
.fa-beat {
  animation: fa-beat 5s ease infinite;
}

.menu-item-active {
  background-color: rgba(255,255,255,.2) !important;
  color: #fff !important;
}

.menu-item-active .q-icon {
  color: #fff !important;
}

@keyframes fa-beat {
  0% {
    transform: scale(1);
  }
  5% {
    transform: scale(1.25);
  }
  20% {
    transform: scale(1);
  }
  30% {
    transform: scale(1);
  }
  35% {
    transform: scale(1.25);
  }
  50% {
    transform: scale(1);
  }
  55% {
    transform: scale(1.25);
  }
  70% {
    transform: scale(1);
  }
}

</style>
