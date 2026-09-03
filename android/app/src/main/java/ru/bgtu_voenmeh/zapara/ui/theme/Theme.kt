package ru.bgtu_voenmeh.zapara.ui.theme

import androidx.compose.material3.Typography
import androidx.compose.material3.darkColorScheme
import androidx.compose.runtime.Composable
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontFamily

// Charon palette 1:1 (see src/Vograph/Themes/Vograph.xaml).
val Obsidian = Color(0xFF0E1013)
val Panel = Color(0xFF15181D)
val PanelAlt = Color(0xFF1B1F26)
val Marble = Color(0xFFC5CAD3)
val MarbleDim = Color(0xFF6B7280)
val Bronze = Color(0xFF6CA5E0)
val Patina = Color(0xFF98C379)
val Cinnabar = Color(0xFFE06C75)
val BorderDim = Color(0xFF262B33)

private val DarkColors = darkColorScheme(
    primary = Bronze,
    onPrimary = Obsidian,
    background = Obsidian,
    onBackground = Marble,
    surface = Panel,
    onSurface = Marble,
    surfaceVariant = PanelAlt,
    onSurfaceVariant = MarbleDim,
    error = Cinnabar,
    onError = Obsidian,
    outline = BorderDim
)

private val MonoTypography: Typography
    get() {
        val base = Typography()
        val mono = FontFamily.Monospace
        return base.copy(
            displayLarge = base.displayLarge.copy(fontFamily = mono),
            displayMedium = base.displayMedium.copy(fontFamily = mono),
            displaySmall = base.displaySmall.copy(fontFamily = mono),
            headlineLarge = base.headlineLarge.copy(fontFamily = mono),
            headlineMedium = base.headlineMedium.copy(fontFamily = mono),
            headlineSmall = base.headlineSmall.copy(fontFamily = mono),
            titleLarge = base.titleLarge.copy(fontFamily = mono),
            titleMedium = base.titleMedium.copy(fontFamily = mono),
            titleSmall = base.titleSmall.copy(fontFamily = mono),
            bodyLarge = base.bodyLarge.copy(fontFamily = mono),
            bodyMedium = base.bodyMedium.copy(fontFamily = mono),
            bodySmall = base.bodySmall.copy(fontFamily = mono),
            labelLarge = base.labelLarge.copy(fontFamily = mono),
            labelMedium = base.labelMedium.copy(fontFamily = mono),
            labelSmall = base.labelSmall.copy(fontFamily = mono)
        )
    }

@Composable
fun ZaparaTheme(content: @Composable () -> Unit) {
    androidx.compose.material3.MaterialTheme(
        colorScheme = DarkColors,
        typography = MonoTypography,
        content = content
    )
}
