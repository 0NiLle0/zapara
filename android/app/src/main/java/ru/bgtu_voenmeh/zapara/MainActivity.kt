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
        // Silent self-update check runs in ScheduleViewModel.init (opt-out via Settings).
        setContent {
            ZaparaTheme {
                val vm: ScheduleViewModel = viewModel(factory = ScheduleVmFactory(application))
                ZaparaApp(vm)
            }
        }
    }
}
