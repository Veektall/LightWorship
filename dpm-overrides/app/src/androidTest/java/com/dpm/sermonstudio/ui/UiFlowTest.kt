package com.dpm.sermonstudio.ui

import androidx.compose.ui.test.assertIsDisplayed
import androidx.compose.ui.test.assertIsEnabled
import androidx.compose.ui.test.junit4.createAndroidComposeRule
import androidx.compose.ui.test.onNodeWithTag
import androidx.compose.ui.test.onNodeWithText
import androidx.compose.ui.test.performClick
import androidx.compose.ui.test.performScrollTo
import org.junit.Rule
import org.junit.Test

class UiFlowTest {
    @get:Rule
    val rule = createAndroidComposeRule<MainActivity>()

    @Test
    fun completeMockProductionJourney() {
        rule.onNodeWithText("Good evening, Victor").assertIsDisplayed()
        rule.onNodeWithTag("primary_home_action").performClick()
        rule.onNodeWithText("Add sermon recordings").assertIsDisplayed()
        rule.onNodeWithTag("sources_continue").assertIsEnabled().performClick()
        rule.onNodeWithText("Build and review transcript").assertIsDisplayed()
        rule.onNodeWithTag("transcript_continue").performClick()
        rule.onNodeWithText("Shape the listening experience").assertIsDisplayed()
        rule.onNodeWithTag("studio_continue").performScrollTo().performClick()
        rule.onNodeWithText("Finish and share").assertIsDisplayed()
        rule.onNodeWithTag("render_button").performClick()
        rule.onNodeWithText("Sermon ready").assertIsDisplayed()
    }

    @Test
    fun emptyProjectCannotSkipSourceRequirement() {
        rule.onNodeWithTag("nav_sources").performClick()
        rule.onNodeWithTag("sources_continue").assertIsEnabled()
    }

    @Test
    fun settingsRoundTripReturnsHome() {
        rule.onNodeWithTag("settings_button").performClick()
        rule.onNodeWithText("Optional AI assistance").assertIsDisplayed()
        rule.onNodeWithTag("settings_back").performClick()
        rule.onNodeWithText("Good evening, Victor").assertIsDisplayed()
    }
}
