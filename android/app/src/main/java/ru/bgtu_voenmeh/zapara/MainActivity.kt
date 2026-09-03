package ru.bgtu_voenmeh.zapara

import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.compose.material3.Text

// A1 stub — full schedule UI lands in Phase A2.
class MainActivity : ComponentActivity() {
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        setContent { Text("ЗАПАРА") }
    }
}
