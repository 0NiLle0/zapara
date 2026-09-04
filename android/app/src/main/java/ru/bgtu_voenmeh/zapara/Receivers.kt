package ru.bgtu_voenmeh.zapara

import android.content.BroadcastReceiver
import android.content.Context
import android.content.Intent
import ru.bgtu_voenmeh.zapara.data.Notifications

// Fired by AlarmManager at the user's notification times (off-main-thread via goAsync).
class NotificationReceiver : BroadcastReceiver() {
    override fun onReceive(context: Context, intent: Intent) {
        val pending = goAsync()
        Thread {
            try {
                Notifications.showForTime(context.applicationContext, intent.getStringExtra("time"))
            } catch (_: Exception) {
            } finally {
                pending.finish()
            }
        }.start()
    }
}

// Re-arm alarms after reboot (one-shot daily alarms don't survive it).
class BootReceiver : BroadcastReceiver() {
    override fun onReceive(context: Context, intent: Intent) {
        if (intent.action != Intent.ACTION_BOOT_COMPLETED) return
        val pending = goAsync()
        Thread {
            try {
                Notifications.schedule(context.applicationContext)
            } catch (_: Exception) {
            } finally {
                pending.finish()
            }
        }.start()
    }
}
