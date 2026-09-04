package ru.bgtu_voenmeh.zapara

import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.lifecycle.viewmodel.compose.viewModel
import ru.bgtu_voenmeh.zapara.ui.ScheduleViewModel
import ru.bgtu_voenmeh.zapara.ui.ScheduleVmFactory
import ru.bgtu_voenmeh.zapara.ui.ZaparaApp
import ru.bgtu_voenmeh.zapara.ui.theme.ZaparaTheme

class MainActivity : ComponentActivity() {
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        // temp auto-update from git: silent check on launch
        checkUpdateSilently()
        setContent {
            ZaparaTheme {
                val vm: ScheduleViewModel = viewModel(factory = ScheduleVmFactory(application))
                ZaparaApp(vm)
            }
        }
    }

    private fun checkUpdateSilently() {
        Thread {
            try {
                val info = ru.bgtu_voenmeh.zapara.data.AutoUpdate.getLatest("android")
                if (info != null && ru.bgtu_voenmeh.zapara.data.AutoUpdate.isNewer(info.tag)) {
                    runOnUiThread {
                        android.widget.Toast.makeText(this, "Доступно обновление ${info.tag} — откройте Настройки → Проверить обновление", android.widget.Toast.LENGTH_LONG).show()
                    }
                }
            } catch (_: Exception) {}
        }.start()
    }
}
