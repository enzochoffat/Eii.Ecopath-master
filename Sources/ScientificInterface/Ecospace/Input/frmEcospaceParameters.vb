' ===============================================================================
' This file is part of Ecopath with Ecosim (EwE)
'
' EwE is free software: you can redistribute it and/or modify it under the terms
' of the GNU General Public License version 2 as published by the Free Software 
' Foundation.
'
' EwE is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; 
' without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR 
' PURPOSE. See the GNU General Public License for more details.
'
' You should have received a copy of the GNU General Public License along with EwE.
' If not, see <http://www.gnu.org/licenses/gpl-2.0.html>. 
'
' Copyright 1991- 
'    Ecopath International Initiative, Barcelona, Spain
' ===============================================================================
'

#Region " Imports "

Option Strict On

Imports EwECore
Imports EwEUtils.Core
Imports EwEUtils.Logging
Imports EwEUtils.SystemUtilities
Imports Microsoft.Extensions.Logging
Imports System.Globalization
Imports System.Diagnostics
Imports System.IO
Imports ScientificInterfaceShared.Commands
Imports SharedResources = ScientificInterfaceShared.My.Resources

#End Region ' Imports

Namespace Ecospace

    ''' -----------------------------------------------------------------------
    ''' <summary>
    ''' Form from which to configure generic Ecospace parameters.
    ''' </summary>
    ''' -----------------------------------------------------------------------
    Public Class frmEcospaceParameters

        Public Const NOTSAVEDEXT As String = "-notsaved-"

#Region " Private vars "

        ' Scenario generics
        Private m_fpScenarioName As cEwEFormatProvider = Nothing
        Private m_fpScenarioDescription As cEwEFormatProvider = Nothing
        Private m_fpAuthor As cEwEFormatProvider = Nothing
        Private m_fpContact As cEwEFormatProvider = Nothing

        ' Threading
        Private m_fpNGridThreads As cEwEFormatProvider = Nothing
        Private m_fpNBiomassThreads As cEwEFormatProvider = Nothing
        Private m_fpNEffortThreads As cEwEFormatProvider = Nothing
        Private m_fpNumPackets As cEwEFormatProvider = Nothing

        ' Model
        Private m_fpTotalTime As cEwEFormatProvider = Nothing
        Private m_fpNumTSpYear As cEwEFormatProvider = Nothing
        Private m_fpTolerance As cEwEFormatProvider = Nothing
        Private m_fpSOR As cEwEFormatProvider = Nothing
        Private m_fpMaxIterations As cEwEFormatProvider = Nothing
        Private m_fpUseExact As cEwEFormatProvider = Nothing
        Private m_fpAnnualOutput As cEwEFormatProvider = Nothing
        Private m_fpFitResponseType As cEwEFormatProvider = Nothing

        Private m_fpMovePackets As cEwEFormatProvider = Nothing
        Private m_fpAllowHabCapGradCalc As cEwEFormatProvider = Nothing
        Private m_fpMinCapacity As cEwEFormatProvider = Nothing
        Private WithEvents m_bpConTracing As cBooleanProperty = Nothing

        ' Ecospace time series
        Private WithEvents m_bpUseBiomassForcing As cBooleanProperty = Nothing
        Private m_fpUseBiomassForcing As cEwEFormatProvider = Nothing
        Private WithEvents m_bpUseDiscardForcing As cBooleanProperty = Nothing
        Private m_fpUseDiscardForcing As cEwEFormatProvider = Nothing

        ' Properties to monitor for setting radio button check states
        Private WithEvents m_bpUseIBM As cBooleanProperty = Nothing
        Private WithEvents m_bpUseNewStanza As cBooleanProperty = Nothing
        Private WithEvents m_bpAdjustSpace As cBooleanProperty = Nothing
        Private WithEvents m_bpEffort As cBooleanProperty = Nothing

        'Spatial distribution penalty
        Private m_fpUsePenaltySearch As cEwEFormatProvider = Nothing
        Private m_fpPenPow As cEwEFormatProvider = Nothing
        Private m_fpEffortAdjust As cEwEFormatProvider = Nothing
        Private m_fpFirstMonthPenalty As cEwEFormatProvider = Nothing
        Private m_fpEffortRelax As cEwEFormatProvider = Nothing
        Private m_fpFirstOutputTimestep As cEwEFormatProvider = Nothing
        Private m_fpAutosaveVisibleGroupsFleetsOnly As cEwEFormatProvider = Nothing

        'Use other model
        Private WithEvents m_bpUseOtherModel As cBooleanProperty = Nothing
        Private m_fpUseOtherModel As cEwEFormatProvider = Nothing

        ' Logging
        Private ReadOnly m_logger As ILogger = LoggingContext.CreateLogger(Of frmEcospaceParameters)()
        Private ReadOnly m_fibeLogSync As New Object()

        ' FIBE coupling: fleet selection for the fleets managed by FIBE
        Private WithEvents m_clbFIBEFleets As CheckedListBox = Nothing

        ' FIBE coupling: temporal agent numbers file (CSV/Excel per date/fleet type)
        Private WithEvents m_lblAgentNumbersFile As Label = Nothing
        Private WithEvents m_tbxAgentNumbersFile As TextBox = Nothing
        Private WithEvents m_btnAgentNumbersFile As Button = Nothing
        Private WithEvents m_btnClearAgentNumbersFile As Button = Nothing
        Private m_IsLayingOutTemporalAgentControls As Boolean = False

        ' FIBE coupling: restricted areas tab
        Private WithEvents m_tcMain As TabControl = Nothing
        Private WithEvents m_tpEcospace As TabPage = Nothing
        Private WithEvents m_tpRestrictedZones As TabPage = Nothing
        Private WithEvents m_dgvRestrictedZones As DataGridView = Nothing
        Private WithEvents m_btnAddZone As Button = Nothing
        Private WithEvents m_btnRemoveZone As Button = Nothing
        Private WithEvents m_btnBrowseZone As Button = Nothing
        Private WithEvents m_cbVectorFleet As ComboBox = Nothing
        Private WithEvents m_cbVectorYear As ComboBox = Nothing
        Private WithEvents m_btnImportVector As Button = Nothing
        Private WithEvents m_dgvVector As DataGridView = Nothing
        Private m_cfgRestrictedAreas As cRestrictedAreasConfig = Nothing
        Private m_bLoadingVector As Boolean = False
        Private Const c_AllYearsDisplay As String = "All"

        ' FIBE coupling: initial agents tab (one row per agent, step 1 only)
        Private WithEvents m_tpAgents As TabPage = Nothing
        Private WithEvents m_dgvAgents As DataGridView = Nothing
        Private WithEvents m_btnAddAgent As Button = Nothing
        Private WithEvents m_btnRemoveAgent As Button = Nothing
        Private WithEvents m_btnImportAgents As Button = Nothing
        Private WithEvents m_btnExportAgents As Button = Nothing
        Private WithEvents m_lblAgentCounts As Label = Nothing
        Private m_cfgAgents As cFibeAgentsConfig = Nothing
        Private m_bLoadingAgents As Boolean = False

#End Region ' Private vars

#Region " Form events "

        Public Sub New()
            Me.InitializeComponent()
        End Sub

        ''' -------------------------------------------------------------------
        ''' <summary>
        ''' Event handler; called when the form is initially loaded.
        ''' </summary>
        ''' -------------------------------------------------------------------
        Protected Overrides Sub OnLoad(e As System.EventArgs)

            MyBase.OnLoad(e)

            If (Me.UIContext Is Nothing) Then Return

            Dim parms As cEcospaceModelParameters = Me.Core.EcospaceModelParameters()
            Dim bm As cEcospaceBasemap = Me.Core.EcospaceBasemap
            Dim propMan As cPropertyManager = Me.PropertyManager

            ' Start listening to props
            Me.m_bpUseIBM = DirectCast(propMan.GetProperty(parms, eVarNameFlags.UseIBM), cBooleanProperty)
            Me.m_bpUseNewStanza = DirectCast(propMan.GetProperty(parms, eVarNameFlags.UseNewMultiStanza), cBooleanProperty)
            Me.m_bpAdjustSpace = DirectCast(propMan.GetProperty(parms, eVarNameFlags.AdjustSpace), cBooleanProperty)
            Me.m_bpEffort = DirectCast(propMan.GetProperty(parms, eVarNameFlags.PredictEffort), cBooleanProperty)

            Me.m_bpConTracing = DirectCast(propMan.GetProperty(parms, eVarNameFlags.ConSimOnEcoSpace), cBooleanProperty)

            Me.m_bpUseBiomassForcing = DirectCast(propMan.GetProperty(parms, eVarNameFlags.EcospaceUseEcosimBiomassForcing), cBooleanProperty)
            Me.m_fpUseBiomassForcing = New cPropertyFormatProvider(Me.UIContext, Me.m_cbUseEcosimBiomassForcing, Me.m_bpUseBiomassForcing)

            Me.m_bpUseDiscardForcing = DirectCast(propMan.GetProperty(parms, eVarNameFlags.EcospaceUseEcosimDiscardForcing), cBooleanProperty)
            Me.m_fpUseDiscardForcing = New cPropertyFormatProvider(Me.UIContext, Me.m_cbUseEcosimDiscardForcing, Me.m_bpUseDiscardForcing)

            Me.m_bpUseOtherModel = DirectCast(propMan.GetProperty(parms, eVarNameFlags.UseOtherModel), cBooleanProperty)
            Me.m_fpUseOtherModel = New cPropertyFormatProvider(Me.UIContext, Me.m_Couplage, Me.m_bpUseOtherModel)

            Me.ConfigureTemporalAgentContainer()

            ' FIBE coupling: build the fleet selection list
            Me.InitializeFIBEFleetList()

            ' FIBE coupling: temporal agent numbers file (CSV/Excel per fleet/date)
            Me.InitializeTemporalAgentControls()
            Me.LoadAgentNumbersFile()

            ' FIBE coupling: restricted areas tab
            Me.InitializeRestrictedAreasTab()
            Me.m_cfgRestrictedAreas = parms.GetRestrictedAreasConfig()
            Me.LoadRestrictedZones()

            ' FIBE coupling: initial agents tab (step 1 situation)
            Me.m_cfgAgents = parms.GetFibeAgentsConfig()
            Me.LoadAgents()

            Me.m_clbAutosave.Items.Clear()
            For n As Integer = 1 To parms.nResultWriters
                Dim writer As IEcospaceResultsWriter = parms.ResultWriter(n)
                Me.m_clbAutosave.Items.Add(writer, writer.Enabled)
            Next

            'Me.UpdateControls()

            ' Hmm, connecting one control to three live properties - this could be dangerous
            Me.m_fpNGridThreads = New cPropertyFormatProvider(Me.UIContext, Me.m_nudNumThreads, parms, eVarNameFlags.nGridSolverThreads)
            Me.m_fpNBiomassThreads = New cPropertyFormatProvider(Me.UIContext, Me.m_nudNumThreads, parms, eVarNameFlags.nSpaceThreads)
            Me.m_fpNEffortThreads = New cPropertyFormatProvider(Me.UIContext, Me.m_nudNumThreads, parms, eVarNameFlags.nEffortDistThreads)
            Me.m_fpNumPackets = New cPropertyFormatProvider(Me.UIContext, Me.m_tbNumPackets, parms, eVarNameFlags.PacketsMultiplier)
            Me.m_fpFirstOutputTimestep = New cPropertyFormatProvider(Me.UIContext, Me.m_nudFirstTimeStep, parms, eVarNameFlags.EcospaceAutosaveFirstTimeStep)

            Me.m_fpAutosaveVisibleGroupsFleetsOnly = New cPropertyFormatProvider(Me.UIContext, Me.m_cbAutosaveVisibleOnly, parms, eVarNameFlags.EcospaceAutosaveSelectedGroupsFleetsOnly)

            ' Model
            Me.m_fpTotalTime = New cPropertyFormatProvider(Me.UIContext, Me.m_tbTotalTime, parms, eVarNameFlags.TotalTime)
            Me.m_fpNumTSpYear = New cPropertyFormatProvider(Me.UIContext, Me.m_tbNumTimeStepsPerYear, parms, eVarNameFlags.NumTimeStepsPerYear)
            Me.m_fpTolerance = New cPropertyFormatProvider(Me.UIContext, Me.m_tbTolerance, parms, eVarNameFlags.Tolerance)
            Me.m_fpSOR = New cPropertyFormatProvider(Me.UIContext, Me.m_tbSOR, parms, eVarNameFlags.SOR)
            Me.m_fpMaxIterations = New cPropertyFormatProvider(Me.UIContext, Me.m_nudMaxIterations, parms, eVarNameFlags.MaxIterations)
            Me.m_fpUseExact = New cPropertyFormatProvider(Me.UIContext, Me.m_cbUseExact, parms, eVarNameFlags.UseExact)
            Me.m_fpAnnualOutput = New cPropertyFormatProvider(Me.UIContext, Me.m_cbAnnualOutput, parms, eVarNameFlags.EcospaceAutosaveAnnualOutput)
            Me.m_fpMovePackets = New cPropertyFormatProvider(Me.UIContext, Me.m_cbMovePackets, parms, eVarNameFlags.EcospaceIBMMovePacketOnStanza)
            Me.m_fpMinCapacity = New cPropertyFormatProvider(Me.UIContext, Me.m_tbxMinCap, parms, eVarNameFlags.EcospaceMinForagingCapacity)
            Me.m_fpAllowHabCapGradCalc = New cPropertyFormatProvider(Me.UIContext, Me.m_cbCalcHabCapGrad, parms, eVarNameFlags.EcospaceUseHabCapGradCorrections)

            Me.m_fpUsePenaltySearch = New cPropertyFormatProvider(Me.UIContext, Me.m_cbUsePenalty, parms, eVarNameFlags.EcospaceDoPenaltySearch)
            Me.m_fpPenPow = New cPropertyFormatProvider(Me.UIContext, Me.m_tbPenPow, parms, eVarNameFlags.EcospacePenpow)
            Me.m_fpEffortAdjust = New cPropertyFormatProvider(Me.UIContext, Me.m_tbEffortAdjustWeight, parms, eVarNameFlags.EcospaceNoFishWeight)
            Me.m_fpFirstMonthPenalty = New cPropertyFormatProvider(Me.UIContext, Me.m_tbFirstPenaltyMonth, parms, eVarNameFlags.EcospaceFirstPenaltyMonth)
            Me.m_fpEffortRelax = New cPropertyFormatProvider(Me.UIContext, Me.m_tbPredEffortRelax, parms, eVarNameFlags.EcospaceEffortRelaxationWeight)

            Me.UpdateScenarioFormatProviders()

            Me.CoreComponents = New eCoreComponentType() {eCoreComponentType.Ecospace, eCoreComponentType.Core, eCoreComponentType.TimeSeries}

            Me.UpdateControls()

            If (Me.Core IsNot Nothing) Then
                Me.Core.AddEcospaceTimeStepHandler(AddressOf Me.OnEcospaceTimeStep)
            End If

        End Sub

        Protected Overrides Sub OnFormClosed(e As System.Windows.Forms.FormClosedEventArgs)

            Try

                If (Me.Core IsNot Nothing) Then
                    Me.Core.RemoveEcospaceTimeStepHandler(AddressOf Me.OnEcospaceTimeStep)
                End If

                Me.m_bpUseIBM = Nothing
                Me.m_bpUseNewStanza = Nothing
                Me.m_bpAdjustSpace = Nothing
                Me.m_bpConTracing = Nothing
                Me.m_bpEffort = Nothing

                Me.m_fpScenarioName.Release()
                Me.m_fpScenarioDescription.Release()
                Me.m_fpAuthor.Release()
                Me.m_fpContact.Release()

                Me.m_fpNGridThreads.Release()
                Me.m_fpNBiomassThreads.Release()
                Me.m_fpNEffortThreads.Release()
                Me.m_fpNumPackets.Release()
                Me.m_fpTotalTime.Release()
                Me.m_fpNumTSpYear.Release()
                Me.m_fpTolerance.Release()
                Me.m_fpSOR.Release()
                Me.m_fpMaxIterations.Release()
                Me.m_fpUseExact.Release()
                Me.m_fpMovePackets.Release()
                Me.m_fpMinCapacity.Release()
                Me.m_fpAllowHabCapGradCalc.Release()
                Me.m_fpUseBiomassForcing.Release()
                Me.m_fpUseDiscardForcing.Release()
                Me.m_fpAnnualOutput.Release()

                Me.m_fpPenPow.Release()
                Me.m_fpEffortAdjust.Release()
                Me.m_fpFirstMonthPenalty.Release()
                Me.m_fpEffortRelax.Release()
                Me.m_fpUsePenaltySearch.Release()

                Me.m_fpFirstOutputTimestep.Release()
                Me.m_fpAutosaveVisibleGroupsFleetsOnly.Release()

                Me.m_fpUseOtherModel.Release()


            Catch ex As Exception

            End Try

            MyBase.OnFormClosed(e)

        End Sub

#End Region ' Form events

#Region " Form content handling "

        ''' -------------------------------------------------------------------
        ''' <summary>
        ''' Helper enum, used to determine the threading model type from ecospace data flags.
        ''' </summary>
        ''' -------------------------------------------------------------------
        Private Enum eThreadingModelType As Integer
            UseNewStanza
            UseIBM
            OldSchool
        End Enum

        Private m_bInUpdate As Boolean = False

        ''' -------------------------------------------------------------------
        ''' <summary>
        ''' Update and enable controls that cannot be managed any other way.
        ''' </summary>
        ''' -------------------------------------------------------------------
        Protected Overrides Sub UpdateControls()

            If (Me.UIContext Is Nothing) Then Return
            If (Me.m_bInUpdate) Then Return

            Me.m_bInUpdate = True

            Dim threadingModel As eThreadingModelType = eThreadingModelType.OldSchool
            Dim bUseIBM As Boolean = CBool(Me.m_bpUseIBM.GetValue())
            Dim bUseNewStanza As Boolean = CBool(Me.m_bpUseNewStanza.GetValue())
            Dim parms As cEcospaceModelParameters = Me.Core.EcospaceModelParameters

            If bUseIBM Then threadingModel = eThreadingModelType.UseIBM
            If bUseNewStanza Then threadingModel = eThreadingModelType.UseNewStanza

            Select Case threadingModel
                Case eThreadingModelType.OldSchool
                    Me.m_rbOldSchool.Checked = True
                Case eThreadingModelType.UseIBM
                    Me.m_rbIBM.Checked = True
                Case eThreadingModelType.UseNewStanza
                    Me.m_rbNewStanzaModel.Checked = True
            End Select

            Me.m_rbBaseBiomass.Checked = Not CBool(Me.m_bpAdjustSpace.GetValue())
            Me.m_rbAdjustedBiomass.Checked = CBool(Me.m_bpAdjustSpace.GetValue())

            Me.m_cbContaminantTracing.Checked = CBool(Me.m_bpConTracing.GetValue())

            For i As Integer = 0 To Me.m_clbAutosave.Items.Count - 1
                Dim wr As IEcospaceResultsWriter = DirectCast(Me.m_clbAutosave.Items(i), IEcospaceResultsWriter)
                Me.m_clbAutosave.SetItemChecked(i, wr.Enabled And Me.Core.Autosave(eAutosaveTypes.EcospaceResults))
            Next

            ' Time series
            Dim manager As EcospaceTimeSeries.cEcospaceTimeSeriesManager = Me.Core.EcospaceTimeSeriesManager
            Me.m_tbxXYTimeSeriesFile.Text = If(String.IsNullOrWhiteSpace(manager.BiomassInputFileName), SharedResources.GENERIC_VALUE_NOTSET, manager.BiomassInputFileName)
            Me.m_tbxlOutputResidualsFile.Text = If(String.IsNullOrWhiteSpace(manager.OutputFileName), SharedResources.GENERIC_VALUE_NOTSET, manager.OutputFileName)

            ' Ecosim forcing
            Me.m_fpUseBiomassForcing.Enabled = Me.Core.EcospaceModelParameters.IsEcosimBiomassForcingLoaded
            Me.m_cbUseEcosimDiscardForcing.Visible = True
            Me.m_fpUseDiscardForcing.Enabled = Me.Core.EcospaceModelParameters.IsEcosimDiscardForcingLoaded

            Me.m_rbPredictEffort.Checked = CBool(Me.m_bpEffort.GetValue())
            Me.m_rbEcopathEffort.Checked = Not CBool(Me.m_bpEffort.GetValue())

            Me.m_bInUpdate = False

        End Sub

#End Region ' Form content handling

#Region " cProperty events "

        ''' -------------------------------------------------------------------
        ''' <summary>
        ''' Event handler; called when either of the two model state properties changes.
        ''' </summary>
        ''' <param name="prop">The property that changed.</param>
        ''' <param name="changeFlags">The extent of the change.</param>
        ''' -------------------------------------------------------------------
        Private Sub OnPropertyChanged(prop As cProperty, changeFlags As cProperty.eChangeFlags) _
            Handles m_bpUseIBM.PropertyChanged, m_bpUseNewStanza.PropertyChanged, m_bpConTracing.PropertyChanged,
                    m_bpUseBiomassForcing.PropertyChanged, m_bpUseDiscardForcing.PropertyChanged
            Me.UpdateControls()
        End Sub

#End Region ' cProperty events

#Region " Control events "

        ''' -------------------------------------------------------------------
        ''' <summary>
        ''' Event handler; called when the IBM mode radio button is checked.
        ''' </summary>
        ''' -------------------------------------------------------------------
        Private Sub OnRunIBM(sender As Object, e As System.EventArgs) _
            Handles m_rbIBM.CheckedChanged, m_rbNewStanzaModel.CheckedChanged, m_rbOldSchool.CheckedChanged

            If (Me.UIContext Is Nothing) Then Return
            If (Me.m_bInUpdate) Then Return

            If Me.m_rbIBM.Checked Then
                Me.m_bpUseNewStanza.SetValue(False)
                Me.m_bpUseIBM.SetValue(True)
            ElseIf Me.m_rbNewStanzaModel.Checked Then
                Me.m_bpUseIBM.SetValue(False)
                Me.m_bpUseNewStanza.SetValue(True)
            ElseIf Me.m_rbOldSchool.Checked Then
                Me.m_bpUseNewStanza.SetValue(False)
                Me.m_bpUseIBM.SetValue(False)
            End If

        End Sub

        Private Sub OnBiomassOptionChanged(sender As Object, e As System.EventArgs) _
            Handles m_rbBaseBiomass.CheckedChanged, m_rbAdjustedBiomass.CheckedChanged

            If (Me.UIContext Is Nothing) Then Return
            If (Me.m_bInUpdate) Then Return

            Me.m_bpAdjustSpace.SetValue(Me.m_rbAdjustedBiomass.Checked)

        End Sub

        Private Sub OnEffortOptionChanged(sender As Object, e As System.EventArgs) _
            Handles m_rbPredictEffort.CheckedChanged, m_rbEcopathEffort.CheckedChanged

            If (Me.UIContext Is Nothing) Then Return
            If (Me.m_bInUpdate) Then Return

            Me.m_bpEffort.SetValue(Me.m_rbPredictEffort.Checked)

        End Sub

        Private Sub OnConcTracingOptionChanged(sender As Object, e As System.EventArgs) _
            Handles m_cbContaminantTracing.CheckedChanged

            If (Me.UIContext Is Nothing) Then Return
            If (Me.m_bInUpdate) Then Return

            If Me.m_cbContaminantTracing.Checked Then
                Dim cmdh As cCommandHandler = Me.CommandHandler
                Dim cmd As cCommand = cmdh.GetCommand("EnableEcotracer")

                If (cmd IsNot Nothing) Then
                    cmd.Tag = eTracerRunModeTypes.RunSpace
                    cmd.Invoke()
                    If (Me.Core.ActiveEcotracerScenarioIndex <= 0) Then
                        Me.m_cbContaminantTracing.Checked = False
                    End If
                End If
            End If

            ' If tracer scenario loaded turn this on
            Me.m_bpConTracing.SetValue(Me.m_cbContaminantTracing.Checked)

            Me.UpdateControls()

        End Sub

        Private Sub m_clbAutosave_Format(sender As Object, e As System.Windows.Forms.ListControlConvertEventArgs) _
            Handles m_clbAutosave.Format
            Try
                e.Value = DirectCast(e.ListItem, IEcospaceResultsWriter).DisplayName
            Catch ex As Exception

            End Try
        End Sub

        Private Sub m_clbAutosave_ItemCheck(sender As Object, e As System.Windows.Forms.ItemCheckEventArgs) _
            Handles m_clbAutosave.ItemCheck

            If Me.m_bInUpdate Then Return

            ' Delay the update, because the item state has not changed yet
            Me.BeginInvoke(New MethodInvoker(AddressOf Me.UpdateResultWriters))

        End Sub

        Private Sub UpdateResultWriters()

            Dim bAutoSaving As Boolean = False

            If Me.m_bInUpdate Then Return
            Me.m_bInUpdate = True

            For i As Integer = 0 To Me.m_clbAutosave.Items.Count - 1
                Dim wr As IEcospaceResultsWriter = DirectCast(Me.m_clbAutosave.Items(i), IEcospaceResultsWriter)
                wr.Enabled = Me.m_clbAutosave.GetItemChecked(i)
                bAutoSaving = bAutoSaving Or wr.Enabled
            Next
            Me.Core.Autosave(eAutosaveTypes.EcospaceResults) = bAutoSaving

            Me.m_bInUpdate = False

        End Sub

        Private Sub OnLoadXYTimeSeries_Click(sender As Object, e As EventArgs) Handles m_btnLoadXYTimeSeries.Click

            Dim cmdh As cCommandHandler = Me.UIContext.CommandHandler
            Dim cmdFO As cFileOpenCommand = DirectCast(cmdh.GetCommand(cFileOpenCommand.COMMAND_NAME), cFileOpenCommand)

            cmdFO.Invoke(SharedResources.FILEFILTER_CSV & "|" & SharedResources.FILEFILTER_XYZ & "|" & SharedResources.FILEFILTER_TEXT)
            If cmdFO.Result = System.Windows.Forms.DialogResult.OK Then
                Dim manager As EcospaceTimeSeries.cEcospaceTimeSeriesManager = Me.Core.EcospaceTimeSeriesManager
                Dim InputFile As String = cmdFO.FileNames(0)
                manager.Load(InputFile, "", eVarNameFlags.EcospaceMapBiomass)
            End If
        End Sub

        Private Sub OnTimeSeriesOutputFile_Click(sender As Object, e As EventArgs) Handles m_btnTimeSeriesOutputFile.Click
            Dim manager As EcospaceTimeSeries.cEcospaceTimeSeriesManager = Me.Core.EcospaceTimeSeriesManager
            Dim dlgSave As New SaveFileDialog

            dlgSave.Filter = SharedResources.FILEFILTER_CSV & "|" & SharedResources.FILEFILTER_XYZ & "|" & SharedResources.FILEFILTER_TEXT
            dlgSave.InitialDirectory = IO.Path.GetDirectoryName(manager.OutputFileName)
            dlgSave.FileName = IO.Path.GetFileName(manager.OutputFileName)
            If dlgSave.ShowDialog = System.Windows.Forms.DialogResult.OK Then
                manager.OutputFileName = dlgSave.FileName
            End If

        End Sub

        Private Sub OnUseOtherModelCheckedChanged(sender As Object, e As EventArgs) Handles m_Couplage.CheckedChanged

            If (Me.UIContext Is Nothing) Then Return
            If (Me.m_bInUpdate) Then Return
            Console.WriteLine("Use other model: " & Me.m_Couplage.Checked)

            If (Me.m_bpUseOtherModel IsNot Nothing) Then
                Me.m_bpUseOtherModel.SetValue(Me.m_Couplage.Checked)
            End If

            ' FIBE coupling: show/hide the fleet selection and temporal agent file
            If Me.m_clbFIBEFleets IsNot Nothing Then
                Me.m_clbFIBEFleets.Visible = Me.m_Couplage.Checked
            End If
            Me.UpdateTemporalAgentControlsVisibility()

        End Sub

        ''' -------------------------------------------------------------------
        ''' <summary>
        ''' Build the list of Ecospace fleets with a checkbox per fleet, to select
        ''' the fleets that are managed by the FIBE coupling.
        ''' </summary>
        ''' -------------------------------------------------------------------
        Private Sub InitializeFIBEFleetList()

            If (Me.Core Is Nothing) Then Return
            Dim ds As cEcospaceDataStructures = Me.Core.EcospaceDataStructures
            If (ds Is Nothing) Then Return

            Me.m_clbFIBEFleets = New CheckedListBox()
            Me.m_clbFIBEFleets.CheckOnClick = True
            Me.m_clbFIBEFleets.BorderStyle = BorderStyle.FixedSingle
            Me.m_clbFIBEFleets.IntegralHeight = False
            Me.m_clbFIBEFleets.Location = New Point(10, 40)
            Me.m_clbFIBEFleets.Width = Me.m_plUseOtherModel.ClientSize.Width - 20
            Me.m_clbFIBEFleets.Height = 120
            Me.m_clbFIBEFleets.Anchor = AnchorStyles.Top Or AnchorStyles.Left Or AnchorStyles.Right
            Me.m_clbFIBEFleets.Visible = Me.m_Couplage.Checked
            Me.m_clbFIBEFleets.Name = "m_clbFIBEFleets"
            Me.m_clbFIBEFleets.AccessibleName = "FIBE fleets"

            For iFleet As Integer = 1 To ds.nFleets
                Dim strFleetName As String = ""
                Try
                    strFleetName = Me.Core.EcopathFleetInputs(iFleet).Name
                Catch ex As Exception
                    strFleetName = "Fleet " & iFleet.ToString
                End Try
                Me.m_clbFIBEFleets.Items.Add(strFleetName, ds.isFIBEFleetManaged(iFleet))
            Next

            Me.m_plUseOtherModel.Controls.Add(Me.m_clbFIBEFleets)
            Me.m_plUseOtherModel.Controls.SetChildIndex(Me.m_clbFIBEFleets, 0)

            ' Fit the list in the visible area of the window: the panel sits in the last
            ' AutoSize row of the form layout, a fixed 120px list would extend below the
            ' window edge. When the available space is smaller than the content, the list
            ' scrolls internally so every fleet stays reachable.
            Dim nAvailable As Integer = Me.ClientSize.Height - (Me.m_plUseOtherModel.Top + Me.m_clbFIBEFleets.Top) - 40
            Me.m_clbFIBEFleets.Height = Math.Max(40, Math.Min(120, nAvailable))

        End Sub

        ' -------------------------------------------------------------------
        ' Configure le conteneur qui doit pouvoir grandir lorsque les
        ' contrôles temporels sont ajoutés.
        ' -------------------------------------------------------------------
        Private Sub ConfigureTemporalAgentContainer()

            If Me.m_tlpStuff Is Nothing Then Return
            If Me.m_plUseOtherModel Is Nothing Then Return

            Me.SuspendLayout()
            Me.m_tlpStuff.SuspendLayout()

            Try

                ' Le formulaire doit pouvoir afficher une zone plus grande
                ' que sa zone visible.
                Me.AutoScroll = True

                ' Le TableLayoutPanel doit grandir verticalement.
                Me.m_tlpStuff.AutoSize = True
                Me.m_tlpStuff.AutoSizeMode = AutoSizeMode.GrowAndShrink
                Me.m_tlpStuff.Dock = DockStyle.Top

                ' Le panel occupe toute la cellule de la ligne 5.
                Me.m_plUseOtherModel.AutoSize = False
                Me.m_plUseOtherModel.Dock = DockStyle.Fill

                ' La ligne 5 sera pilotée explicitement par le layout.
                If Me.m_tlpStuff.RowStyles.Count > 5 Then

                    Me.m_tlpStuff.RowStyles(5).SizeType =
                        SizeType.Absolute

                End If

            Finally

                Me.m_tlpStuff.ResumeLayout(True)
                Me.ResumeLayout(True)

            End Try

        End Sub

        ''' -------------------------------------------------------------------
        ''' <summary>
        ''' Build the controls that let the user pick the CSV/Excel file defining
        ''' the temporal number of agents per fleet type.
        '''
        ''' <para>
        ''' Fichier attendu (<c>Agents_number.csv</c> — malgré l'extension, c'est
        ''' souvent un <c>.xlsx</c>) :
        ''' <list type="bullet">
        '''   <item><description>Col A = date (sérial Excel ou ISO, une ligne par pas de temps)</description></item>
        '''   <item><description>Cols B.. = une colonne par type de flottille 
        '''   (<c>archipelago</c>, <c>coastal</c>, <c>trawler</c>) avec le nombre
        '''   d'agents à instancier à cette date.</description></item>
        ''' </list>
        ''' Ce fichier sera lu par le couplage FIBE à l'initialisation et à
        ''' chaque pas de temps pour faire varier le nombre d'agents. Cette
        ''' méthode ne fait que l'UI (sélection + persistance du chemin) —
        ''' le parsing/usage moteur viendra en phase 2.
        ''' </para>
        '''
        ''' <para>Layout : label + TextBox readonly + bouton "Temporal number agent"
        ''' + bouton "Clear", tous ancrés pour se redimensionner avec le panel
        ''' <c>m_plUseOtherModel</c>. Leur visibilité suit la checkbox FIBE.</para>
        ''' </summary>
        ''' -------------------------------------------------------------------
        Private Sub InitializeTemporalAgentControls()

            If Me.m_plUseOtherModel Is Nothing Then Return

            ' Éviter une double création des contrôles
            If Me.m_lblAgentNumbersFile IsNot Nothing Then Return

            Me.m_plUseOtherModel.SuspendLayout()

            Try

                ' ---------------------------------------------------------
                ' LABEL
                ' ---------------------------------------------------------
                Me.m_lblAgentNumbersFile = New Label()

                With Me.m_lblAgentNumbersFile
                    .Name = "m_lblAgentNumbersFile"
                    .Text =
                        "Temporal agent numbers file (per fleet type / date):"
                    .AutoSize = False
                    .Anchor =
                        AnchorStyles.Top Or
                        AnchorStyles.Left Or
                        AnchorStyles.Right
                    .Visible = Me.m_Couplage.Checked
                End With


                ' ---------------------------------------------------------
                ' TEXTBOX
                ' ---------------------------------------------------------
                Me.m_tbxAgentNumbersFile = New TextBox()

                With Me.m_tbxAgentNumbersFile
                    .Name = "m_tbxAgentNumbersFile"
                    .ReadOnly = True
                    .BorderStyle = BorderStyle.FixedSingle
                    .BackColor = SystemColors.Window
                    .Anchor =
                        AnchorStyles.Top Or
                        AnchorStyles.Left Or
                        AnchorStyles.Right
                    .Visible = Me.m_Couplage.Checked
                End With


                ' ---------------------------------------------------------
                ' BOUTON BROWSE
                ' ---------------------------------------------------------
                Me.m_btnAgentNumbersFile = New Button()

                With Me.m_btnAgentNumbersFile
                    .Name = "m_btnAgentNumbersFile"
                    .Text = "Temporal number agent"
                    .AutoSize = False
                    .Anchor =
                        AnchorStyles.Top Or
                        AnchorStyles.Right
                    .Visible = Me.m_Couplage.Checked
                End With


                ' ---------------------------------------------------------
                ' BOUTON CLEAR
                ' ---------------------------------------------------------
                Me.m_btnClearAgentNumbersFile = New Button()

                With Me.m_btnClearAgentNumbersFile
                    .Name = "m_btnClearAgentNumbersFile"
                    .Text = "Clear"
                    .AutoSize = False
                    .Anchor =
                        AnchorStyles.Top Or
                        AnchorStyles.Right
                    .Visible = Me.m_Couplage.Checked
                End With


                ' ---------------------------------------------------------
                ' AJOUT AU PANEL
                ' ---------------------------------------------------------
                Me.m_plUseOtherModel.Controls.Add(
                    Me.m_lblAgentNumbersFile
                )

                Me.m_plUseOtherModel.Controls.Add(
                    Me.m_tbxAgentNumbersFile
                )

                Me.m_plUseOtherModel.Controls.Add(
                    Me.m_btnAgentNumbersFile
                )

                Me.m_plUseOtherModel.Controls.Add(
                    Me.m_btnClearAgentNumbersFile
                )


                ' Mettre les nouveaux contrôles au premier plan
                Me.m_lblAgentNumbersFile.BringToFront()
                Me.m_tbxAgentNumbersFile.BringToFront()
                Me.m_btnAgentNumbersFile.BringToFront()
                Me.m_btnClearAgentNumbersFile.BringToFront()

            Finally

                Me.m_plUseOtherModel.ResumeLayout(True)

            End Try


            ' Premier calcul
            Me.LayoutTemporalAgentControls()


            ' Recalcul lorsque la largeur du panel change
            AddHandler Me.m_plUseOtherModel.SizeChanged,
                AddressOf Me.OnTemporalAgentPanelResize


            ' Recalcul après le premier affichage réel
            If Me.IsHandleCreated Then

                Me.BeginInvoke(
                    New MethodInvoker(
                        Sub()
                            Me.LayoutTemporalAgentControls()
                        End Sub
                    )
                )

            End If

        End Sub

        ''' -------------------------------------------------------------------
        ''' <summary>
        ''' (Re)position the temporal-agent controls to fill the width of
        ''' <c>m_plUseOtherModel</c>. Called at creation and on every resize.
        ''' </summary>
        ''' -------------------------------------------------------------------
        Private Sub LayoutTemporalAgentControls()

            If m_IsLayingOutTemporalAgentControls Then Return

            If Me.m_plUseOtherModel Is Nothing Then Return
            If Me.m_tlpStuff Is Nothing Then Return

            If Me.m_tbxAgentNumbersFile Is Nothing Then Return
            If Me.m_btnAgentNumbersFile Is Nothing Then Return
            If Me.m_btnClearAgentNumbersFile Is Nothing Then Return
            If Me.m_lblAgentNumbersFile Is Nothing Then Return
            If Me.m_clbFIBEFleets Is Nothing Then Return


            Dim panelWidth As Integer =
                Me.m_plUseOtherModel.ClientSize.Width

            If panelWidth <= 0 Then Return


            m_IsLayingOutTemporalAgentControls = True

            Me.m_plUseOtherModel.SuspendLayout()
            Me.m_tlpStuff.SuspendLayout()

            Try

                Dim margin As Integer = 10
                Dim gap As Integer = 6

                Dim clearWidth As Integer = 70
                Dim browseWidth As Integer = 175

                Dim buttonHeight As Integer = 25


                ' ---------------------------------------------------------
                ' POSITION DU LABEL
                ' ---------------------------------------------------------

                Dim labelTop As Integer =
                    Me.m_clbFIBEFleets.Bottom + 10


                Me.m_lblAgentNumbersFile.AutoSize = False

                Me.m_lblAgentNumbersFile.Location =
                    New Point(
                        margin,
                        labelTop
                    )

                Me.m_lblAgentNumbersFile.Size =
                    New Size(
                        Math.Max(
                            100,
                            panelWidth - (margin * 2)
                        ),
                        22
                    )


                ' ---------------------------------------------------------
                ' TEXTBOX
                ' ---------------------------------------------------------

                Dim textTop As Integer =
                    Me.m_lblAgentNumbersFile.Bottom + 4


                Me.m_tbxAgentNumbersFile.Location =
                    New Point(
                        margin,
                        textTop
                    )


                Me.m_tbxAgentNumbersFile.Size =
                    New Size(
                        Math.Max(
                            100,
                            panelWidth - (margin * 2)
                        ),
                        23
                    )


                ' ---------------------------------------------------------
                ' BOUTONS
                ' ---------------------------------------------------------

                Dim buttonTop As Integer =
                    Me.m_tbxAgentNumbersFile.Bottom + 6


                ' Clear complètement à droite
                Me.m_btnClearAgentNumbersFile.Size =
                    New Size(
                        clearWidth,
                        buttonHeight
                    )


                Me.m_btnClearAgentNumbersFile.Location =
                    New Point(
                        panelWidth - margin - clearWidth,
                        buttonTop
                    )


                ' Bouton Browse à gauche du bouton Clear
                Me.m_btnAgentNumbersFile.Size =
                    New Size(
                        browseWidth,
                        buttonHeight
                    )


                Me.m_btnAgentNumbersFile.Location =
                    New Point(
                        Me.m_btnClearAgentNumbersFile.Left -
                        gap -
                        browseWidth,
                        buttonTop
                    )


                ' ---------------------------------------------------------
                ' CALCUL DE LA HAUTEUR NECESSAIRE
                ' ---------------------------------------------------------

                Dim neededHeight As Integer =
                    Me.m_btnClearAgentNumbersFile.Bottom + 10


                ' Ne jamais réduire en dessous des contrôles existants
                Dim minimumHeight As Integer = 0

                If Me.m_hdrUseOtherModel IsNot Nothing Then

                    minimumHeight =
                        Math.Max(
                            minimumHeight,
                            Me.m_hdrUseOtherModel.Bottom + 5
                        )

                End If


                If Me.m_Couplage IsNot Nothing Then

                    minimumHeight =
                        Math.Max(
                            minimumHeight,
                            Me.m_Couplage.Bottom + 5
                        )

                End If


                If Me.m_clbFIBEFleets IsNot Nothing Then

                    minimumHeight =
                        Math.Max(
                            minimumHeight,
                            Me.m_clbFIBEFleets.Bottom + 10
                        )

                End If


                neededHeight =
                    Math.Max(
                        neededHeight,
                        minimumHeight
                    )


                ' ---------------------------------------------------------
                ' IMPORTANT :
                ' AGRANDIR LE PANEL
                ' ---------------------------------------------------------

                If Math.Abs(
                    Me.m_plUseOtherModel.Height -
                    neededHeight
                ) > 1 Then

                    Me.m_plUseOtherModel.Height =
                        neededHeight

                End If


                ' ---------------------------------------------------------
                ' IMPORTANT :
                ' AGRANDIR LA LIGNE 5 DU TABLELAYOUTPANEL
                ' ---------------------------------------------------------

                If Me.m_tlpStuff.RowStyles.Count > 5 Then

                    Me.m_tlpStuff.RowStyles(5).SizeType =
                        SizeType.Absolute


                    If Math.Abs(
                        Me.m_tlpStuff.RowStyles(5).Height -
                        CSng(neededHeight)
                    ) > 1.0F Then

                        Me.m_tlpStuff.RowStyles(5).Height =
                            CSng(neededHeight)

                    End If

                End If


                ' Forcer le recalcul de la taille totale
                Me.m_tlpStuff.PerformLayout()

            Finally

                Me.m_tlpStuff.ResumeLayout(True)
                Me.m_plUseOtherModel.ResumeLayout(True)

                m_IsLayingOutTemporalAgentControls = False

            End Try

        End Sub

        ''' <summary>Handles the resize event of the temporal agent panel.</summary>
        ''' <param name="sender">The source of the event.</param>
        ''' <param name="e">The event data.</param>
        Private Sub OnTemporalAgentPanelResize(sender As Object, e As EventArgs)
            Me.LayoutTemporalAgentControls()
        End Sub

        ''' -------------------------------------------------------------------
        ''' <summary>Show/hide the temporal agent controls with the FIBE coupling.</summary>
        ''' -------------------------------------------------------------------
        Private Sub UpdateTemporalAgentControlsVisibility()

            Dim bVisible As Boolean = False

            Try

                bVisible = Me.m_Couplage.Checked

            Catch

                bVisible = False

            End Try


            If Me.m_lblAgentNumbersFile IsNot Nothing Then

                Me.m_lblAgentNumbersFile.Visible =
                    bVisible

            End If


            If Me.m_tbxAgentNumbersFile IsNot Nothing Then

                Me.m_tbxAgentNumbersFile.Visible =
                    bVisible

            End If


            If Me.m_btnAgentNumbersFile IsNot Nothing Then

                Me.m_btnAgentNumbersFile.Visible =
                    bVisible

            End If


            If Me.m_btnClearAgentNumbersFile IsNot Nothing Then

                Me.m_btnClearAgentNumbersFile.Visible =
                    bVisible

            End If


            ' Recalculer la taille et la zone scrollable
            Me.LayoutTemporalAgentControls()


            If Me.m_tlpStuff IsNot Nothing Then

                Me.m_tlpStuff.PerformLayout()

            End If

        End Sub

        ''' -------------------------------------------------------------------
        ''' <summary>
        ''' Load the persisted agent-numbers file path from the core into the
        ''' textbox. Shows <c>&lt;not set&gt;</c> when empty.
        ''' </summary>
        ''' -------------------------------------------------------------------
        Private Sub LoadAgentNumbersFile()

            If (Me.Core Is Nothing) Then Return
            If (Me.m_tbxAgentNumbersFile Is Nothing) Then Return

            Try
                Dim strPath As String = ""
                Try
                    strPath = Me.Core.EcospaceModelParameters.AgentNumbersFile
                Catch ex As Exception
                    strPath = ""
                End Try

                If String.IsNullOrWhiteSpace(strPath) Then
                    Me.m_tbxAgentNumbersFile.Text = SharedResources.GENERIC_VALUE_NOTSET
                    Me.m_tbxAgentNumbersFile.ForeColor = SystemColors.GrayText
                    Me.m_btnClearAgentNumbersFile.Enabled = False
                Else
                    Me.m_tbxAgentNumbersFile.Text = strPath
                    Me.m_tbxAgentNumbersFile.ForeColor = SystemColors.WindowText
                    Me.m_btnClearAgentNumbersFile.Enabled = True
                    ' Warn (non-blocking) if the file no longer exists on disk
                    If (Not File.Exists(strPath)) Then
                        Me.m_logger.LogWarning("Temporal agent file not found on disk: {FilePath}", strPath)
                    End If
                End If
            Catch ex As Exception
                Me.m_logger.LogError(ex, "Failed to load temporal agent file path into UI")
            End Try

        End Sub

        ''' -------------------------------------------------------------------
        ''' <summary>
        ''' Persist the given path into the core (and the underlying
        ''' <c>cEcospaceDataStructures.AgentNumbersFile</c> via sync) and
        ''' refresh the textbox. Called by the browse/clear handlers.
        ''' </summary>
        ''' -------------------------------------------------------------------
        Private Sub SaveAgentNumbersFile(strPath As String)

            If (Me.Core Is Nothing) Then Return

            Dim strSanitized As String = If(strPath, "").Trim()
            Try
                Me.Core.EcospaceModelParameters.AgentNumbersFile = strSanitized
            Catch ex As Exception
                Me.m_logger.LogError(ex, "Failed to persist temporal agent file path: {FilePath}", strSanitized)
            End Try

            Me.LoadAgentNumbersFile()

        End Sub

        ''' -------------------------------------------------------------------
        ''' <summary>
        ''' Handler for the "Temporal number agent..." button. Opens an
        ''' OpenFileDialog that accepts <c>.csv</c>, <c>.xlsx</c>/<c>.xls</c>
        ''' (the sample <c>Agents_number.csv</c> is in fact a ZIP/Excel file)
        ''' and plain text as fallback. Basic validation is done before saving.
        ''' </summary>
        ''' -------------------------------------------------------------------
        Private Sub OnBrowseAgentNumbersFile(sender As Object, e As EventArgs) Handles m_btnAgentNumbersFile.Click

            If (Me.Core Is Nothing) Then Return

            Dim strCurrent As String = ""
            Try
                strCurrent = Me.Core.EcospaceModelParameters.AgentNumbersFile
            Catch
            End Try

            Using dlg As New OpenFileDialog()
                dlg.Title = "Select temporal agent numbers file"
                dlg.Filter = "CSV / Excel files (*.csv;*.xlsx;*.xls)|*.csv;*.xlsx;*.xls|CSV files (*.csv)|*.csv|Excel files (*.xlsx;*.xls)|*.xlsx;*.xls|All files (*.*)|*.*"
                dlg.FilterIndex = 1
                dlg.CheckFileExists = True
                dlg.CheckPathExists = True
                dlg.Multiselect = False
                dlg.RestoreDirectory = True

                If (Not String.IsNullOrWhiteSpace(strCurrent)) Then
                    Try
                        dlg.InitialDirectory = Path.GetDirectoryName(strCurrent)
                        dlg.FileName = Path.GetFileName(strCurrent)
                    Catch
                    End Try
                End If

                If dlg.ShowDialog(Me) <> DialogResult.OK Then Return

                Dim strChosen As String = dlg.FileName

                ' ---- Light validation (non-blocking) ------------------------
                ' We only check that the file can be opened and that its first
                ' line/row contains at least a date column + one fleet column.
                ' Full parsing (Excel serial dates, variable fleet count, time
                ' interpolation) belongs to the engine (phase 2).
                Try
                    Dim fi As New FileInfo(strChosen)
                    If (fi.Length = 0) Then
                        MessageBox.Show(Me,
                                        "The selected file is empty.",
                                        "Temporal agent file",
                                        MessageBoxButtons.OK,
                                        MessageBoxIcon.Warning)
                        Return
                    End If
                Catch ex As Exception
                    Me.m_logger.LogWarning(ex, "Could not stat agent file {FilePath}", strChosen)
                End Try

                Me.SaveAgentNumbersFile(strChosen)

            End Using

        End Sub

        ''' -------------------------------------------------------------------
        ''' <summary>Handler for the "Clear" button — unsets the file path.</summary>
        ''' -------------------------------------------------------------------
        Private Sub OnClearAgentNumbersFile(sender As Object, e As EventArgs) Handles m_btnClearAgentNumbersFile.Click
            Me.SaveAgentNumbersFile("")
        End Sub

        ''' -------------------------------------------------------------------
        ''' <summary>
        ''' Event handler; called when the user toggles the FIBE checkbox of a fleet.
        ''' </summary>
        ''' -------------------------------------------------------------------
        Private Sub OnFIBEFleetItemCheck(sender As Object, e As ItemCheckEventArgs) Handles m_clbFIBEFleets.ItemCheck

            If (Me.Core Is Nothing) Then Return
            Dim ds As cEcospaceDataStructures = Me.Core.EcospaceDataStructures
            If (ds Is Nothing) Then Return

            ' The list items are 0-based, the Ecospace fleets are 1-based
            Dim iFleet As Integer = e.Index + 1
            If (iFleet < 1) Or (iFleet > ds.nFleets) Then Return

            ds.isFIBEFleet(iFleet) = (e.NewValue = CheckState.Checked)

        End Sub

        ''' -------------------------------------------------------------------
        ''' <summary>
        ''' Wrap the existing Ecospace parameter content and the new FIBE
        ''' editors in a three-tab layout. The existing content is moved
        ''' unchanged into the first tab page.
        ''' </summary>
        ''' -------------------------------------------------------------------
        Private Sub InitializeRestrictedAreasTab()

            If (Me.UIContext Is Nothing) Then Return

            Me.m_tcMain = New TabControl()
            Me.m_tcMain.Dock = DockStyle.Fill
            Me.m_tcMain.Name = "m_tcMain"

            Me.m_tpEcospace = New TabPage()
            Me.m_tpEcospace.Text = "Ecospace"
            Me.m_tpEcospace.Dock = DockStyle.Fill
            Me.m_tpEcospace.Name = "m_tpEcospace"

            Me.m_tpRestrictedZones = New TabPage()
            Me.m_tpRestrictedZones.Text = "Restricted zones (FIBE)"
            Me.m_tpRestrictedZones.Dock = DockStyle.Fill
            Me.m_tpRestrictedZones.Name = "m_tpRestrictedZones"

            Me.m_tpAgents = New TabPage()
            Me.m_tpAgents.Text = "Agents (FIBE)"
            Me.m_tpAgents.Dock = DockStyle.Fill
            Me.m_tpAgents.Name = "m_tpAgents"

            ' Move the existing content into the first tab page
            Me.Controls.Remove(Me.m_tlpStuff)
            Me.m_tpEcospace.Controls.Add(Me.m_tlpStuff)
            Me.m_tlpStuff.Dock = DockStyle.Fill

            Me.BuildRestrictedZonesTab()
            Me.BuildAgentsTab()

            Me.m_tcMain.TabPages.Add(Me.m_tpEcospace)
            Me.m_tcMain.TabPages.Add(Me.m_tpRestrictedZones)
            Me.m_tcMain.TabPages.Add(Me.m_tpAgents)
            Me.Controls.Add(Me.m_tcMain)
            Me.m_tcMain.BringToFront()

        End Sub

        ''' -------------------------------------------------------------------
        ''' <summary>
        ''' Build the "Restricted zones (FIBE)" tab content: a grid of named
        ''' zones with their shapefile path, and an add/remove/browse button row,
        ''' plus the per-fleet seasonal restriction grid.
        ''' </summary>
        ''' -------------------------------------------------------------------
        Private Sub BuildRestrictedZonesTab()

            Dim tlpRoot As New TableLayoutPanel()
            tlpRoot.Dock = DockStyle.Fill
            tlpRoot.ColumnCount = 1
            tlpRoot.RowCount = 2
            tlpRoot.RowStyles.Add(New RowStyle(SizeType.Percent, 45))
            tlpRoot.RowStyles.Add(New RowStyle(SizeType.Percent, 55))
            tlpRoot.Padding = New Padding(6)
            tlpRoot.Name = "m_tlpRestrictedZones"

            ' ----------------------------------------------------------------
            ' Section 1: geographic zones
            ' ----------------------------------------------------------------
            Dim gbZones As New GroupBox()
            gbZones.Text = "Restricted areas (shapefiles)"
            gbZones.Dock = DockStyle.Fill
            gbZones.Name = "m_gbRestrictedZones"

            Dim tlpZones As New TableLayoutPanel()
            tlpZones.Dock = DockStyle.Fill
            tlpZones.ColumnCount = 1
            tlpZones.RowCount = 3
            tlpZones.RowStyles.Add(New RowStyle(SizeType.Absolute, 24))
            tlpZones.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
            tlpZones.RowStyles.Add(New RowStyle(SizeType.Absolute, 40))
            tlpZones.Padding = New Padding(6)
            tlpZones.Name = "m_tlpZoneList"

            Dim lbl As New Label()
            lbl.Text = "Geographic zones used as restricted areas by the FIBE coupling (one shapefile per zone)."
            lbl.AutoSize = True
            lbl.Dock = DockStyle.Top
            tlpZones.Controls.Add(lbl, 0, 0)

            Me.m_dgvRestrictedZones = New DataGridView()
            Me.m_dgvRestrictedZones.Dock = DockStyle.Fill
            Me.m_dgvRestrictedZones.AllowUserToAddRows = True
            Me.m_dgvRestrictedZones.AllowUserToDeleteRows = True
            Me.m_dgvRestrictedZones.RowHeadersVisible = False
            Me.m_dgvRestrictedZones.MultiSelect = False
            Me.m_dgvRestrictedZones.SelectionMode = DataGridViewSelectionMode.FullRowSelect
            Me.m_dgvRestrictedZones.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            Me.m_dgvRestrictedZones.EditMode = DataGridViewEditMode.EditOnEnter
            Me.m_dgvRestrictedZones.Name = "m_dgvRestrictedZones"

            Dim colName As New DataGridViewTextBoxColumn()
            colName.HeaderText = "Zone name"
            colName.FillWeight = 30
            Me.m_dgvRestrictedZones.Columns.Add(colName)

            Dim colPath As New DataGridViewTextBoxColumn()
            colPath.HeaderText = "Shapefile path"
            colPath.ReadOnly = True
            colPath.FillWeight = 70
            Me.m_dgvRestrictedZones.Columns.Add(colPath)

            tlpZones.Controls.Add(Me.m_dgvRestrictedZones, 0, 1)

            Dim tlpButtons As New TableLayoutPanel()
            tlpButtons.Dock = DockStyle.Fill
            tlpButtons.ColumnCount = 4
            tlpButtons.RowCount = 1
            tlpButtons.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 90))
            tlpButtons.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 90))
            tlpButtons.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 100))
            tlpButtons.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
            tlpButtons.Name = "m_tlpZoneButtons"

            Me.m_btnAddZone = New Button()
            Me.m_btnAddZone.Text = "Add"
            Me.m_btnAddZone.Width = 80
            Me.m_btnAddZone.Anchor = AnchorStyles.Left Or AnchorStyles.Top
            Me.m_btnAddZone.Name = "m_btnAddZone"
            tlpButtons.Controls.Add(Me.m_btnAddZone, 0, 0)

            Me.m_btnRemoveZone = New Button()
            Me.m_btnRemoveZone.Text = "Remove"
            Me.m_btnRemoveZone.Width = 80
            Me.m_btnRemoveZone.Anchor = AnchorStyles.Left Or AnchorStyles.Top
            Me.m_btnRemoveZone.Name = "m_btnRemoveZone"
            tlpButtons.Controls.Add(Me.m_btnRemoveZone, 1, 0)

            Me.m_btnBrowseZone = New Button()
            Me.m_btnBrowseZone.Text = "Browse..."
            Me.m_btnBrowseZone.Width = 90
            Me.m_btnBrowseZone.Anchor = AnchorStyles.Left Or AnchorStyles.Top
            Me.m_btnBrowseZone.Name = "m_btnBrowseZone"
            tlpButtons.Controls.Add(Me.m_btnBrowseZone, 2, 0)

            tlpZones.Controls.Add(tlpButtons, 0, 2)

            gbZones.Controls.Add(tlpZones)

            ' ----------------------------------------------------------------
            ' Section 2: per-fleet, per-year seasonal restrictions
            ' ----------------------------------------------------------------
            Dim gbVector As New GroupBox()
            gbVector.Text = "Seasonal restrictions (per fleet / per year)"
            gbVector.Dock = DockStyle.Fill
            gbVector.Name = "m_gbRestrictedVector"

            Dim tlpVector As New TableLayoutPanel()
            tlpVector.Dock = DockStyle.Fill
            tlpVector.ColumnCount = 6
            tlpVector.RowCount = 2
            tlpVector.RowStyles.Add(New RowStyle(SizeType.Absolute, 32))
            tlpVector.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
            tlpVector.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 50))
            tlpVector.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 130))
            tlpVector.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 50))
            tlpVector.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 110))
            tlpVector.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 110))
            tlpVector.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
            tlpVector.Padding = New Padding(6)
            tlpVector.Name = "m_tlpVector"

            ' Fleet selector
            Dim lblFleet As New Label()
            lblFleet.Text = "Fleet:"
            lblFleet.Dock = DockStyle.Fill
            lblFleet.TextAlign = ContentAlignment.MiddleLeft
            tlpVector.Controls.Add(lblFleet, 0, 0)

            Me.m_cbVectorFleet = New ComboBox()
            Me.m_cbVectorFleet.Dock = DockStyle.Fill
            Me.m_cbVectorFleet.DropDownStyle = ComboBoxStyle.DropDownList
            Me.m_cbVectorFleet.Name = "m_cbVectorFleet"
            For Each strFleet As String In cRestrictedAreasConfig.FIBEFleetTypes
                Me.m_cbVectorFleet.Items.Add(strFleet)
            Next
            If (Me.m_cbVectorFleet.Items.Count > 0) Then
                Me.m_cbVectorFleet.SelectedIndex = 0
            End If
            tlpVector.Controls.Add(Me.m_cbVectorFleet, 1, 0)

            ' Year filter
            Dim lblYear As New Label()
            lblYear.Text = "Year:"
            lblYear.Dock = DockStyle.Fill
            lblYear.TextAlign = ContentAlignment.MiddleLeft
            tlpVector.Controls.Add(lblYear, 2, 0)

            Me.m_cbVectorYear = New ComboBox()
            Me.m_cbVectorYear.Dock = DockStyle.Fill
            Me.m_cbVectorYear.DropDownStyle = ComboBoxStyle.DropDownList
            Me.m_cbVectorYear.Name = "m_cbVectorYear"
            tlpVector.Controls.Add(Me.m_cbVectorYear, 3, 0)

            ' Import button for sparse interval CSV
            Me.m_btnImportVector = New Button()
            Me.m_btnImportVector.Text = "Import CSV..."
            Me.m_btnImportVector.Dock = DockStyle.Fill
            Me.m_btnImportVector.Name = "m_btnImportVector"
            tlpVector.Controls.Add(Me.m_btnImportVector, 4, 0)

            Me.m_dgvVector = New DataGridView()
            Me.m_dgvVector.Dock = DockStyle.Fill
            Me.m_dgvVector.AllowUserToAddRows = False
            Me.m_dgvVector.AllowUserToDeleteRows = False
            Me.m_dgvVector.AllowUserToResizeRows = False
            Me.m_dgvVector.RowHeadersVisible = True
            Me.m_dgvVector.RowHeadersWidth = 150
            Me.m_dgvVector.MultiSelect = False
            Me.m_dgvVector.SelectionMode = DataGridViewSelectionMode.CellSelect
            Me.m_dgvVector.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            Me.m_dgvVector.EditMode = DataGridViewEditMode.EditOnEnter
            Me.m_dgvVector.Name = "m_dgvVector"

            Dim aMonths() As String = {"Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"}
            For iMonth As Integer = 0 To aMonths.Length - 1
                Dim col As New DataGridViewComboBoxColumn()
                col.HeaderText = aMonths(iMonth)
                col.Name = "m_col" & aMonths(iMonth)
                col.Items.Add(StatusDisplay(eRestrictedAreaStatus.Open))
                col.Items.Add(StatusDisplay(eRestrictedAreaStatus.Navigation))
                col.Items.Add(StatusDisplay(eRestrictedAreaStatus.Closed))
                col.FillWeight = 8
                Me.m_dgvVector.Columns.Add(col)
            Next

            tlpVector.Controls.Add(Me.m_dgvVector, 0, 1)
            tlpVector.SetColumnSpan(Me.m_dgvVector, 6)

            gbVector.Controls.Add(tlpVector)

            tlpRoot.Controls.Add(gbZones, 0, 0)
            tlpRoot.Controls.Add(gbVector, 0, 1)

            Me.m_tpRestrictedZones.Controls.Add(tlpRoot)

        End Sub

        ''' -------------------------------------------------------------------
        ''' <summary>
        ''' Build the "Agents (FIBE)" tab content: a grid with one row per
        ''' initial agent (flottille, name, port, habitats, wave threshold),
        ''' an add/remove/import/export button row and a per-fleet counter.
        ''' </summary>
        ''' -------------------------------------------------------------------
        Private Sub BuildAgentsTab()

            Dim tlpRoot As New TableLayoutPanel()
            tlpRoot.Dock = DockStyle.Fill
            tlpRoot.ColumnCount = 1
            tlpRoot.RowCount = 1
            tlpRoot.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
            tlpRoot.Padding = New Padding(6)
            tlpRoot.Name = "m_tlpAgents"

            Dim gbAgents As New GroupBox()
            gbAgents.Text = "Initial agents (one row per agent; counts evolve via Agents_number.csv)"
            gbAgents.Dock = DockStyle.Fill
            gbAgents.Name = "m_gbAgents"

            Dim tlpAgents As New TableLayoutPanel()
            tlpAgents.Dock = DockStyle.Fill
            tlpAgents.ColumnCount = 1
            tlpAgents.RowCount = 4
            tlpAgents.RowStyles.Add(New RowStyle(SizeType.Absolute, 24))
            tlpAgents.RowStyles.Add(New RowStyle(SizeType.Absolute, 22))
            tlpAgents.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
            tlpAgents.RowStyles.Add(New RowStyle(SizeType.Absolute, 40))
            tlpAgents.Padding = New Padding(6)
            tlpAgents.Name = "m_tlpAgentList"

            Dim lbl As New Label()
            lbl.Text = "Agents modelled by the FIBE coupling (initial situation, static fields kept all run). Import a ';' separated CSV or add rows manually."
            lbl.AutoSize = True
            lbl.Dock = DockStyle.Top
            tlpAgents.Controls.Add(lbl, 0, 0)

            Me.m_lblAgentCounts = New Label()
            Me.m_lblAgentCounts.Text = "0 archipelago / 0 coastal / 0 trawler (total 0)"
            Me.m_lblAgentCounts.AutoSize = True
            Me.m_lblAgentCounts.Dock = DockStyle.Top
            Me.m_lblAgentCounts.Name = "m_lblAgentCounts"
            tlpAgents.Controls.Add(Me.m_lblAgentCounts, 0, 1)

            Me.m_dgvAgents = New DataGridView()
            Me.m_dgvAgents.Dock = DockStyle.Fill
            Me.m_dgvAgents.AllowUserToAddRows = True
            Me.m_dgvAgents.AllowUserToDeleteRows = True
            Me.m_dgvAgents.RowHeadersVisible = False
            Me.m_dgvAgents.MultiSelect = False
            Me.m_dgvAgents.SelectionMode = DataGridViewSelectionMode.FullRowSelect
            Me.m_dgvAgents.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            Me.m_dgvAgents.EditMode = DataGridViewEditMode.EditOnEnter
            Me.m_dgvAgents.Name = "m_dgvAgents"

            Dim colFleet As New DataGridViewComboBoxColumn()
            colFleet.HeaderText = "Flottille"
            colFleet.Name = "m_colAgentFleet"
            colFleet.FillWeight = 18
            For Each strFleet As String In cFibeAgentsConfig.FIBEFleetTypes
                colFleet.Items.Add(strFleet)
            Next
            Me.m_dgvAgents.Columns.Add(colFleet)

            Dim colName As New DataGridViewTextBoxColumn()
            colName.HeaderText = "Name"
            colName.Name = "m_colAgentName"
            colName.FillWeight = 20
            Me.m_dgvAgents.Columns.Add(colName)

            Dim colPort As New DataGridViewTextBoxColumn()
            colPort.HeaderText = "Port number"
            colPort.Name = "m_colAgentPort"
            colPort.FillWeight = 14
            Me.m_dgvAgents.Columns.Add(colPort)

            Dim colHabs As New DataGridViewTextBoxColumn()
            colHabs.HeaderText = "Habitats"
            colHabs.Name = "m_colAgentHabitats"
            colHabs.FillWeight = 28
            Me.m_dgvAgents.Columns.Add(colHabs)

            Dim colWave As New DataGridViewTextBoxColumn()
            colWave.HeaderText = "Wave height threshold"
            colWave.Name = "m_colAgentWave"
            colWave.FillWeight = 20
            Me.m_dgvAgents.Columns.Add(colWave)

            tlpAgents.Controls.Add(Me.m_dgvAgents, 0, 2)

            Dim tlpButtons As New TableLayoutPanel()
            tlpButtons.Dock = DockStyle.Fill
            tlpButtons.ColumnCount = 5
            tlpButtons.RowCount = 1
            tlpButtons.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 90))
            tlpButtons.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 90))
            tlpButtons.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 110))
            tlpButtons.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 110))
            tlpButtons.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
            tlpButtons.Name = "m_tlpAgentButtons"

            Me.m_btnAddAgent = New Button()
            Me.m_btnAddAgent.Text = "Add"
            Me.m_btnAddAgent.Width = 80
            Me.m_btnAddAgent.Anchor = AnchorStyles.Left Or AnchorStyles.Top
            Me.m_btnAddAgent.Name = "m_btnAddAgent"
            tlpButtons.Controls.Add(Me.m_btnAddAgent, 0, 0)

            Me.m_btnRemoveAgent = New Button()
            Me.m_btnRemoveAgent.Text = "Remove"
            Me.m_btnRemoveAgent.Width = 80
            Me.m_btnRemoveAgent.Anchor = AnchorStyles.Left Or AnchorStyles.Top
            Me.m_btnRemoveAgent.Name = "m_btnRemoveAgent"
            tlpButtons.Controls.Add(Me.m_btnRemoveAgent, 1, 0)

            Me.m_btnImportAgents = New Button()
            Me.m_btnImportAgents.Text = "Import CSV..."
            Me.m_btnImportAgents.Width = 100
            Me.m_btnImportAgents.Anchor = AnchorStyles.Left Or AnchorStyles.Top
            Me.m_btnImportAgents.Name = "m_btnImportAgents"
            tlpButtons.Controls.Add(Me.m_btnImportAgents, 2, 0)

            Me.m_btnExportAgents = New Button()
            Me.m_btnExportAgents.Text = "Export CSV..."
            Me.m_btnExportAgents.Width = 100
            Me.m_btnExportAgents.Anchor = AnchorStyles.Left Or AnchorStyles.Top
            Me.m_btnExportAgents.Name = "m_btnExportAgents"
            tlpButtons.Controls.Add(Me.m_btnExportAgents, 3, 0)

            tlpAgents.Controls.Add(tlpButtons, 0, 3)

            gbAgents.Controls.Add(tlpAgents)
            tlpRoot.Controls.Add(gbAgents, 0, 0)
            Me.m_tpAgents.Controls.Add(tlpRoot)

        End Sub

        ''' -------------------------------------------------------------------
        ''' <summary>
        ''' Load the initial agents from the core configuration into the grid.
        ''' </summary>
        ''' -------------------------------------------------------------------
        Private Sub LoadAgents()

            If (Me.m_cfgAgents Is Nothing) Then Return
            If (Me.m_dgvAgents Is Nothing) Then Return

            Me.m_bLoadingAgents = True
            Try
                Me.m_dgvAgents.Rows.Clear()
                For Each agent As cFibeAgent In Me.m_cfgAgents.Agents
                    Me.m_dgvAgents.Rows.Add(
                        agent.Flottille,
                        agent.Name,
                        agent.PortNumber.ToString(CultureInfo.InvariantCulture),
                        String.Join(",", agent.Habitats.ToArray()),
                        agent.WaveThreshold.ToString(CultureInfo.InvariantCulture))
                Next
                Me.UpdateAgentCounts()
            Finally
                Me.m_bLoadingAgents = False
            End Try

        End Sub

        ''' -------------------------------------------------------------------
        ''' <summary>
        ''' Refresh the per-fleet agent counter label from the grid content.
        ''' </summary>
        ''' -------------------------------------------------------------------
        Private Sub UpdateAgentCounts()

            If (Me.m_lblAgentCounts Is Nothing) Then Return
            Dim nArchi As Integer = 0, nCoast As Integer = 0, nTrawl As Integer = 0
            If (Me.m_dgvAgents IsNot Nothing) Then
                For Each row As DataGridViewRow In Me.m_dgvAgents.Rows
                    If row.IsNewRow Then Continue For
                    Dim sFleet As String = If(row.Cells(0).Value Is Nothing, "", row.Cells(0).Value.ToString())
                    Select Case sFleet.Trim().ToLowerInvariant()
                        Case "archipelago", "archipelagos" : nArchi += 1
                        Case "coastal", "coastals" : nCoast += 1
                        Case "trawler", "trawlers" : nTrawl += 1
                        Case "" ' Empty new row, not counted
                        Case Else : nArchi += 0 ' Unknown fleet, reported by SaveAgents
                    End Select
                Next
            End If
            Dim nTotal As Integer = nArchi + nCoast + nTrawl
            Me.m_lblAgentCounts.Text = String.Format(
                "{0} archipelago / {1} coastal / {2} trawler (total {3})",
                nArchi, nCoast, nTrawl, nTotal)

        End Sub

        ''' -------------------------------------------------------------------
        ''' <summary>
        ''' Store the agents from the grid back into the core configuration.
        ''' Rows with an empty name are ignored. Invalid rows are reported
        ''' with a warning and skipped (grid content is preserved).
        ''' </summary>
        ''' <returns>True when the grid holds no blocking error.</returns>
        ''' -------------------------------------------------------------------
        Private Function SaveAgents() As Boolean

            If (Me.m_bLoadingAgents) Then Return True
            If (Me.m_cfgAgents Is Nothing) Then Return True
            If (Me.m_dgvAgents Is Nothing) Then Return True

            Dim agents As New List(Of cFibeAgent)
            Dim seen As New Dictionary(Of String, Integer)(StringComparer.OrdinalIgnoreCase)
            Dim nRow As Integer = 0
            For Each row As DataGridViewRow In Me.m_dgvAgents.Rows
                If row.IsNewRow Then Continue For
                nRow += 1
                Dim sFleet As String = If(row.Cells(0).Value Is Nothing, "", row.Cells(0).Value.ToString()).Trim()
                Dim sName As String = If(row.Cells(1).Value Is Nothing, "", row.Cells(1).Value.ToString()).Trim()
                Dim sPort As String = If(row.Cells(2).Value Is Nothing, "", row.Cells(2).Value.ToString()).Trim()
                Dim sHabs As String = If(row.Cells(3).Value Is Nothing, "", row.Cells(3).Value.ToString()).Trim()
                Dim sWave As String = If(row.Cells(4).Value Is Nothing, "", row.Cells(4).Value.ToString()).Trim()

                ' Skip fully empty rows (allows clearing the grid)
                If (String.IsNullOrEmpty(sFleet) AndAlso String.IsNullOrEmpty(sName) AndAlso
                    String.IsNullOrEmpty(sPort) AndAlso String.IsNullOrEmpty(sHabs) AndAlso
                    String.IsNullOrEmpty(sWave)) Then Continue For

                ' Lenient default: empty fleet falls back to archipelago only when
                ' the user typed a name (avoids blocking manual entry)
                Dim fleet As String = cFibeAgentsConfig.CanonicalFleet(sFleet)
                If (String.IsNullOrEmpty(fleet)) Then
                    m_logger.LogWarning("FIBE coupling: invalid fleet '{Fleet}' at grid row {Row}, row skipped", sFleet, nRow)
                    MessageBox.Show(Me,
                                    String.Format("Invalid fleet '{0}' at row {1}. Expected one of: {2}. Row skipped.",
                                                  sFleet, nRow, String.Join(", ", cFibeAgentsConfig.FIBEFleetTypes)),
                                    "Agents (FIBE)",
                                    MessageBoxButtons.OK,
                                    MessageBoxIcon.Warning)
                    Return False
                End If
                If (String.IsNullOrWhiteSpace(sName)) Then
                    m_logger.LogWarning("FIBE coupling: missing agent name at grid row {Row}, row skipped", nRow)
                    MessageBox.Show(Me,
                                    String.Format("Missing agent name at row {0}. Names must be unique and non-empty.", nRow),
                                    "Agents (FIBE)",
                                    MessageBoxButtons.OK,
                                    MessageBoxIcon.Warning)
                    Return False
                End If
                If (seen.ContainsKey(sName)) Then
                    m_logger.LogWarning("FIBE coupling: duplicate agent name '{Name}' at grid row {Row}", sName, nRow)
                    MessageBox.Show(Me,
                                    String.Format("Duplicate agent name '{0}' at row {1}. Names must be unique.", sName, nRow),
                                    "Agents (FIBE)",
                                    MessageBoxButtons.OK,
                                    MessageBoxIcon.Warning)
                    Return False
                End If
                Dim port As Integer
                If (String.IsNullOrEmpty(sPort)) Then
                    port = 0
                ElseIf (Not Integer.TryParse(sPort, NumberStyles.Integer, CultureInfo.InvariantCulture, port) OrElse port < 0) Then
                    m_logger.LogWarning("FIBE coupling: invalid port number '{Port}' at grid row {Row}", sPort, nRow)
                    MessageBox.Show(Me,
                                    String.Format("Invalid port number '{0}' at row {1}. Expected an integer >= 0.", sPort, nRow),
                                    "Agents (FIBE)",
                                    MessageBoxButtons.OK,
                                    MessageBoxIcon.Warning)
                    Return False
                End If
                Dim wave As Double = 0
                Dim waveOk As Boolean = Double.TryParse(sWave, NumberStyles.Float, CultureInfo.InvariantCulture, wave) AndAlso wave > 0
                If (Not waveOk) Then
                    waveOk = Double.TryParse(sWave.Replace(",", "."), NumberStyles.Float, CultureInfo.InvariantCulture, wave) AndAlso wave > 0
                End If
                If (String.IsNullOrEmpty(sWave)) Then
                    wave = cFibeAgentsConfig.DefaultWaveThreshold
                    waveOk = True
                End If
                If (Not waveOk) Then
                    m_logger.LogWarning("FIBE coupling: invalid wave threshold '{Wave}' at grid row {Row}", sWave, nRow)
                    MessageBox.Show(Me,
                                    String.Format("Invalid wave height threshold '{0}' at row {1}. Expected a number > 0.", sWave, nRow),
                                    "Agents (FIBE)",
                                    MessageBoxButtons.OK,
                                    MessageBoxIcon.Warning)
                    Return False
                End If
                Dim habs As New List(Of String)
                If (Not String.IsNullOrEmpty(sHabs)) Then
                    For Each h As String In sHabs.Split(","c)
                        Dim t As String = If(h, "").Trim()
                        If (String.IsNullOrEmpty(t)) Then Continue For
                        Dim exists As Boolean = False
                        For Each e As String In habs
                            If (String.Equals(e, t, StringComparison.OrdinalIgnoreCase)) Then
                                exists = True
                                Exit For
                            End If
                        Next
                        If (Not exists) Then habs.Add(t)
                    Next
                End If

                seen(sName) = nRow
                agents.Add(New cFibeAgent With {
                    .Flottille = fleet,
                    .Name = sName,
                    .PortNumber = port,
                    .Habitats = habs,
                    .WaveThreshold = wave
                })
            Next

            Me.m_cfgAgents.Agents = agents
            Me.PersistAgents()
            Me.UpdateAgentCounts()
            Return True

        End Function

        ''' -------------------------------------------------------------------
        ''' <summary>
        ''' Write the agents configuration to the core.
        ''' </summary>
        ''' -------------------------------------------------------------------
        Private Sub PersistAgents()

            If (Me.m_cfgAgents Is Nothing) Then Return
            If (Me.Core Is Nothing) Then Return

            Dim parms As cEcospaceModelParameters = Me.Core.EcospaceModelParameters()
            parms.SetFibeAgentsConfig(Me.m_cfgAgents)
            m_logger.LogInformation("FIBE coupling: persisted {Count} initial agents", Me.m_cfgAgents.Agents.Count)

        End Sub

        ''' -------------------------------------------------------------------
        ''' <summary>Add a new empty agent row and focus it.</summary>
        ''' -------------------------------------------------------------------
        Private Sub OnAddAgent(sender As Object, e As EventArgs) Handles m_btnAddAgent.Click

            If (Me.m_dgvAgents Is Nothing) Then Return

            Dim iRow As Integer = Me.m_dgvAgents.Rows.Add("", "", "", "", "")
            Me.m_dgvAgents.CurrentCell = Me.m_dgvAgents.Rows(iRow).Cells(0)
            Me.m_dgvAgents.BeginEdit(False)

        End Sub

        ''' -------------------------------------------------------------------
        ''' <summary>Remove the selected agent row.</summary>
        ''' -------------------------------------------------------------------
        Private Sub OnRemoveAgent(sender As Object, e As EventArgs) Handles m_btnRemoveAgent.Click

            If (Me.m_dgvAgents Is Nothing) Then Return
            If (Me.m_dgvAgents.CurrentRow Is Nothing) Then Return
            If (Me.m_dgvAgents.CurrentRow.IsNewRow) Then Return

            Me.m_dgvAgents.Rows.Remove(Me.m_dgvAgents.CurrentRow)
            Me.SaveAgents()

        End Sub

        ''' -------------------------------------------------------------------
        ''' <summary>
        ''' Handle the Import CSV button: pick a per-agent file
        ''' (flottille;name;port number;habitats;wave height threshold),
        ''' REPLACE the current grid content, and report errors with line numbers.
        ''' </summary>
        ''' -------------------------------------------------------------------
        Private Sub OnImportAgents(sender As Object, e As EventArgs) Handles m_btnImportAgents.Click
            If (Me.m_cfgAgents Is Nothing) Then Return

            Using dlg As New OpenFileDialog()
                dlg.Title = "Import FIBE initial agents"
                dlg.Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*"
                dlg.FilterIndex = 1
                dlg.CheckFileExists = True
                dlg.CheckPathExists = True
                dlg.Multiselect = False
                dlg.RestoreDirectory = True
                If (dlg.ShowDialog(Me) <> DialogResult.OK) Then Return

                Dim candidate As New cFibeAgentsConfig()
                Dim err As String = ""
                If (candidate.ImportCsv(dlg.FileName, err)) Then
                    Me.m_cfgAgents = candidate
                    Me.PersistAgents()
                    Me.LoadAgents()
                    m_logger.LogInformation("FIBE coupling: imported {Count} initial agents from {File}", candidate.Agents.Count, dlg.FileName)
                    MessageBox.Show(Me,
                                    String.Format("Import successful: {0} agent(s) loaded from {1}.", candidate.Agents.Count, Path.GetFileName(dlg.FileName)),
                                    "Import agents",
                                    MessageBoxButtons.OK,
                                    MessageBoxIcon.Information)
                Else
                    m_logger.LogError("FIBE coupling: agent import failed for {File}: {Error}", dlg.FileName, err)
                    MessageBox.Show(Me,
                                    err,
                                    "Import failed",
                                    MessageBoxButtons.OK,
                                    MessageBoxIcon.Error)
                End If
            End Using
        End Sub

        ''' -------------------------------------------------------------------
        ''' <summary>Handle the Export CSV button: save the grid as a per-agent CSV.</summary>
        ''' -------------------------------------------------------------------
        Private Sub OnExportAgents(sender As Object, e As EventArgs) Handles m_btnExportAgents.Click
            If (Me.m_cfgAgents Is Nothing) Then Return
            If (Not Me.SaveAgents()) Then Return

            Using dlg As New SaveFileDialog()
                dlg.Title = "Export FIBE initial agents"
                dlg.Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*"
                dlg.FilterIndex = 1
                dlg.RestoreDirectory = True
                dlg.FileName = "fibe_agents.csv"
                If (dlg.ShowDialog(Me) <> DialogResult.OK) Then Return
                Try
                    Me.m_cfgAgents.ExportCsv(dlg.FileName)
                    m_logger.LogInformation("FIBE coupling: exported {Count} initial agents to {File}", Me.m_cfgAgents.Agents.Count, dlg.FileName)
                    MessageBox.Show(Me,
                                    String.Format("Export successful: {0} agent(s) written to {1}.", Me.m_cfgAgents.Agents.Count, Path.GetFileName(dlg.FileName)),
                                    "Export agents",
                                    MessageBoxButtons.OK,
                                    MessageBoxIcon.Information)
                Catch ex As Exception
                    m_logger.LogError(ex, "FIBE coupling: failed to export agents to {File}", dlg.FileName)
                    MessageBox.Show(Me,
                                    String.Format("Export failed: {0}", ex.Message),
                                    "Export failed",
                                    MessageBoxButtons.OK,
                                    MessageBoxIcon.Error)
                End Try
            End Using
        End Sub

        ''' -------------------------------------------------------------------
        ''' <summary>Persist the agents whenever the user finishes editing a cell.</summary>
        ''' -------------------------------------------------------------------
        Private Sub OnAgentsCellEndEdit(sender As Object, e As DataGridViewCellEventArgs) _
            Handles m_dgvAgents.CellEndEdit
            Me.SaveAgents()
        End Sub

        ''' -------------------------------------------------------------------
        ''' <summary>Persist the agents when the user deletes a row directly in the grid.</summary>
        ''' -------------------------------------------------------------------
        Private Sub OnAgentsUserDeletedRow(sender As Object, e As DataGridViewRowEventArgs) _
            Handles m_dgvAgents.UserDeletedRow
            Me.SaveAgents()
        End Sub

        ''' -------------------------------------------------------------------
        ''' <summary>
        ''' Swallow combo-box formatting errors (e.g. empty fleet cell on a new
        ''' row). The row is validated later by <see cref="SaveAgents"/>.
        ''' </summary>
        ''' -------------------------------------------------------------------
        Private Sub OnAgentsDataError(sender As Object, e As DataGridViewDataErrorEventArgs) _
            Handles m_dgvAgents.DataError
            e.ThrowException = False
            e.Cancel = False
        End Sub

        ''' -------------------------------------------------------------------
        ''' <summary>
        ''' Load the restricted zones from the core configuration into the grid.
        ''' </summary>
        ''' -------------------------------------------------------------------
        Private Sub LoadRestrictedZones()

            If (Me.m_cfgRestrictedAreas Is Nothing) Then Return
            If (Me.m_dgvRestrictedZones Is Nothing) Then Return

            Me.m_dgvRestrictedZones.Rows.Clear()
            For Each zone As cRestrictedAreaZone In Me.m_cfgRestrictedAreas.Zones
                Me.m_dgvRestrictedZones.Rows.Add(zone.Name, zone.ShapefilePath)
            Next

            Me.SyncVectorToZones()

        End Sub

        ''' -------------------------------------------------------------------
        ''' <summary>
        ''' Store the restricted zones from the grid back into the core
        ''' configuration.
        ''' </summary>
        ''' -------------------------------------------------------------------
        Private Sub SaveRestrictedZones()

            If (Me.m_cfgRestrictedAreas Is Nothing) Then Return
            If (Me.m_dgvRestrictedZones Is Nothing) Then Return

            Me.m_cfgRestrictedAreas.Zones.Clear()
            For Each row As DataGridViewRow In Me.m_dgvRestrictedZones.Rows
                If row.IsNewRow Then Continue For
                Dim strName As String = If(row.Cells(0).Value Is Nothing, "", row.Cells(0).Value.ToString())
                Dim strPath As String = If(row.Cells(1).Value Is Nothing, "", row.Cells(1).Value.ToString())
                If (Not String.IsNullOrWhiteSpace(strName)) OrElse (Not String.IsNullOrWhiteSpace(strPath)) Then
                    Me.m_cfgRestrictedAreas.Zones.Add(New cRestrictedAreaZone With {.Name = strName, .ShapefilePath = strPath})
                End If
            Next

            Me.SyncVectorToZones()
            Me.PersistRestrictedAreas()

        End Sub

        ''' -------------------------------------------------------------------
        ''' <summary>
        ''' Write the restricted areas configuration to the core.
        ''' </summary>
        ''' -------------------------------------------------------------------
        Private Sub PersistRestrictedAreas()

            If (Me.m_cfgRestrictedAreas Is Nothing) Then Return
            If (Me.Core Is Nothing) Then Return

            Dim parms As cEcospaceModelParameters = Me.Core.EcospaceModelParameters()
            parms.SetRestrictedAreasConfig(Me.m_cfgRestrictedAreas)

        End Sub

        ''' -------------------------------------------------------------------
        ''' <summary>
        ''' Add a new zone row with a default name.
        ''' </summary>
        ''' -------------------------------------------------------------------
        Private Sub OnAddZone(sender As Object, e As EventArgs) Handles m_btnAddZone.Click

            If (Me.m_dgvRestrictedZones Is Nothing) Then Return

            Dim nNext As Integer = 1
            Dim nExisting As Integer = Me.m_dgvRestrictedZones.Rows.Count
            For n As Integer = 1 To nExisting + 1
                Dim bTaken As Boolean = False
                For Each row As DataGridViewRow In Me.m_dgvRestrictedZones.Rows
                    If row.IsNewRow Then Continue For
                    If (row.Cells(0).Value IsNot Nothing) AndAlso (String.Equals(row.Cells(0).Value.ToString(), "zone_" & n.ToString())) Then
                        bTaken = True
                        Exit For
                    End If
                Next
                If Not bTaken Then
                    nNext = n
                    Exit For
                End If
            Next

            Dim iRow As Integer = Me.m_dgvRestrictedZones.Rows.Add("zone_" & nNext.ToString(), "")
            Me.m_dgvRestrictedZones.CurrentCell = Me.m_dgvRestrictedZones.Rows(iRow).Cells(0)
            Me.m_dgvRestrictedZones.BeginEdit(False)

        End Sub

        ''' -------------------------------------------------------------------
        ''' <summary>
        ''' Remove the selected zone row.
        ''' </summary>
        ''' -------------------------------------------------------------------
        Private Sub OnRemoveZone(sender As Object, e As EventArgs) Handles m_btnRemoveZone.Click

            If (Me.m_dgvRestrictedZones Is Nothing) Then Return
            If (Me.m_dgvRestrictedZones.CurrentRow Is Nothing) Then Return
            If (Me.m_dgvRestrictedZones.CurrentRow.IsNewRow) Then Return

            Me.m_dgvRestrictedZones.Rows.Remove(Me.m_dgvRestrictedZones.CurrentRow)
            Me.SaveRestrictedZones()

        End Sub

        ''' -------------------------------------------------------------------
        ''' <summary>
        ''' Browse for a shapefile and assign it to the selected zone row.
        ''' </summary>
        ''' -------------------------------------------------------------------
        Private Sub OnBrowseZone(sender As Object, e As EventArgs) Handles m_btnBrowseZone.Click

            If (Me.m_dgvRestrictedZones Is Nothing) Then Return
            If (Me.m_dgvRestrictedZones.CurrentRow Is Nothing) Then Return

            Dim row As DataGridViewRow = Me.m_dgvRestrictedZones.CurrentRow
            If row.IsNewRow Then
                row = Me.m_dgvRestrictedZones.Rows(Me.m_dgvRestrictedZones.Rows.Add("", ""))
            End If

            Dim dlg As New OpenFileDialog()
            dlg.Filter = "Shapefile (*.shp)|*.shp|All files (*.*)|*.*"
            dlg.CheckFileExists = True
            Dim strCurrent As String = If(row.Cells(1).Value Is Nothing, "", row.Cells(1).Value.ToString())
            If (Not String.IsNullOrWhiteSpace(strCurrent)) Then
                dlg.InitialDirectory = IO.Path.GetDirectoryName(strCurrent)
                dlg.FileName = IO.Path.GetFileName(strCurrent)
            End If

            If dlg.ShowDialog(Me) = System.Windows.Forms.DialogResult.OK Then
                row.Cells(1).Value = dlg.FileName
                Me.SaveRestrictedZones()
            End If

        End Sub

        ''' -------------------------------------------------------------------
        ''' <summary>
        ''' Persist the zones whenever the user finishes editing a cell.
        ''' </summary>
        ''' -------------------------------------------------------------------
        Private Sub OnRestrictedZonesCellEndEdit(sender As Object, e As DataGridViewCellEventArgs) _
            Handles m_dgvRestrictedZones.CellEndEdit
            Me.SaveRestrictedZones()
        End Sub

        ''' -------------------------------------------------------------------
        ''' <summary>
        ''' Persist the zones when the user deletes a row directly in the grid.
        ''' </summary>
        ''' -------------------------------------------------------------------
        Private Sub OnRestrictedZonesUserDeletedRow(sender As Object, e As DataGridViewRowEventArgs) _
            Handles m_dgvRestrictedZones.UserDeletedRow
            Me.SaveRestrictedZones()
        End Sub

        ''' -------------------------------------------------------------------
        ''' <summary>
        ''' Display text for a restriction status.
        ''' </summary>
        ''' -------------------------------------------------------------------
        Private Shared Function StatusDisplay(status As eRestrictedAreaStatus) As String
            Select Case status
                Case eRestrictedAreaStatus.Navigation
                    Return "Navigation"
                Case eRestrictedAreaStatus.Closed
                    Return "Closed"
                Case Else
                    Return "Open"
            End Select
        End Function

        ''' -------------------------------------------------------------------
        ''' <summary>
        ''' Status from its display text.
        ''' </summary>
        ''' -------------------------------------------------------------------
        Private Shared Function StatusFromDisplay(strDisplay As String) As eRestrictedAreaStatus
            Select Case strDisplay
                Case "Navigation"
                    Return eRestrictedAreaStatus.Navigation
                Case "Closed"
                    Return eRestrictedAreaStatus.Closed
                Case Else
                    Return eRestrictedAreaStatus.Open
            End Select
        End Function

        ''' -------------------------------------------------------------------
        ''' <summary>
        ''' Return the inclusive simulation year range [firstYear, lastYear]
        ''' derived from the core (EcosimFirstYear + TotalTime). Used to
        ''' size the per-year restriction matrices and the year filter.
        ''' </summary>
        ''' -------------------------------------------------------------------
        Private Function GetSimulationYearRange() As Tuple(Of Integer, Integer)
            Dim firstYear As Integer = 2019
            Dim lastYear As Integer = 2019
            Try
                If (Me.Core IsNot Nothing) Then
                    firstYear = Me.Core.EcosimFirstYear()
                    If (firstYear <= 0) Then firstYear = DateTime.Now.Year
                    Dim totalYears As Integer = 20
                    Dim parms As cEcospaceModelParameters = Me.Core.EcospaceModelParameters()
                    Dim obj As Object = Nothing
                    Try
                        obj = parms.GetVariable(eVarNameFlags.TotalTime)
                    Catch
                    End Try
                    If (obj IsNot Nothing) Then
                        Dim f As Single = 0
                        If (Single.TryParse(obj.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, f)) Then
                            totalYears = CInt(Math.Ceiling(f))
                        ElseIf (Not Integer.TryParse(obj.ToString(), totalYears)) Then
                            totalYears = 20
                        End If
                    ElseIf (Me.m_tbTotalTime IsNot Nothing) Then
                        Dim t As String = Me.m_tbTotalTime.Text.Replace("""", "").Trim()
                        If (Not Integer.TryParse(t, totalYears)) Then totalYears = 20
                    End If
                    If (totalYears < 1) Then totalYears = 1
                    lastYear = firstYear + totalYears - 1
                ElseIf (Me.m_cfgRestrictedAreas IsNot Nothing) Then
                    Dim yrs As List(Of Integer) = Me.m_cfgRestrictedAreas.GetAllYears()
                    If (yrs.Count > 0) Then
                        firstYear = yrs(0)
                        lastYear = yrs(yrs.Count - 1)
                    End If
                End If
            Catch
            End Try
            If (lastYear < firstYear) Then lastYear = firstYear
            Return Tuple.Create(firstYear, lastYear)
        End Function

        ''' -------------------------------------------------------------------
        ''' <summary>
        ''' Populate the year filter combo with "All" plus each simulation year.
        ''' Preserves the current selection when possible.
        ''' </summary>
        ''' -------------------------------------------------------------------
        Private Sub UpdateVectorYearFilter()
            If (Me.m_cbVectorYear Is Nothing) Then Return
            Dim yrRange As Tuple(Of Integer, Integer) = Me.GetSimulationYearRange()
            Dim firstYear As Integer = yrRange.Item1
            Dim lastYear As Integer = yrRange.Item2
            Dim prevSel As String = c_AllYearsDisplay
            If (Me.m_cbVectorYear.SelectedItem IsNot Nothing) Then
                prevSel = Me.m_cbVectorYear.SelectedItem.ToString()
            End If
            Me.m_cbVectorYear.Items.Clear()
            Me.m_cbVectorYear.Items.Add(c_AllYearsDisplay)
            For yr As Integer = firstYear To lastYear
                Me.m_cbVectorYear.Items.Add(yr.ToString())
            Next
            Dim idx As Integer = Me.m_cbVectorYear.Items.IndexOf(prevSel)
            If (idx >= 0) Then
                Me.m_cbVectorYear.SelectedIndex = idx
            Else
                Me.m_cbVectorYear.SelectedIndex = 0
            End If
        End Sub

        ''' -------------------------------------------------------------------
        ''' <summary>
        ''' Synchronize the per-fleet, per-year restriction matrices with the
        ''' current zone list and simulation year range. Grows or shrinks every
        ''' matrix so that row i matches zone i. New cells default to open.
        ''' </summary>
        ''' -------------------------------------------------------------------
        Private Sub SyncVectorToZones()

            If (Me.m_cfgRestrictedAreas Is Nothing) Then Return

            Dim nZones As Integer = Me.m_cfgRestrictedAreas.Zones.Count
            Dim yrRange As Tuple(Of Integer, Integer) = Me.GetSimulationYearRange()
            Dim firstYear As Integer = yrRange.Item1
            Dim lastYear As Integer = yrRange.Item2

            ' Ensure matrices exist for every fleet/year in the simulation
            Me.m_cfgRestrictedAreas.EnsureYearRange(firstYear, lastYear, nZones)

            ' Resize any out-of-range years that may exist from imports
            For Each fleetDict As Dictionary(Of Integer, Integer()()) In Me.m_cfgRestrictedAreas.Vector.Values
                Dim years As New List(Of Integer)(fleetDict.Keys)
                For Each yr As Integer In years
                    Dim mat As Integer()() = fleetDict(yr)
                    If (mat Is Nothing OrElse mat.Length <> nZones) Then
                        Dim resized(nZones - 1)() As Integer
                        For i As Integer = 0 To nZones - 1
                            If (mat IsNot Nothing AndAlso i < mat.Length AndAlso mat(i) IsNot Nothing) Then
                                resized(i) = mat(i)
                                If (resized(i).Length <> cRestrictedAreasConfig.nMonths) Then
                                    resized(i) = New Integer(cRestrictedAreasConfig.nMonths - 1) {}
                                    For mm As Integer = 0 To cRestrictedAreasConfig.nMonths - 1
                                        resized(i)(mm) = CInt(eRestrictedAreaStatus.Open)
                                    Next
                                End If
                            Else
                                resized(i) = New Integer(cRestrictedAreasConfig.nMonths - 1) {}
                                For mm As Integer = 0 To cRestrictedAreasConfig.nMonths - 1
                                    resized(i)(mm) = CInt(eRestrictedAreaStatus.Open)
                                Next
                            End If
                        Next
                        fleetDict(yr) = resized
                    End If
                Next
            Next

            Me.UpdateVectorYearFilter()
            Me.LoadVectorGrid()

        End Sub

        ''' -------------------------------------------------------------------
        ''' <summary>
        ''' (Re)load the restriction grid for the currently selected fleet and
        ''' year filter. When "All" is selected, rows are zone × year stacked
        ''' (e.g. "zone_1 [2019]"), otherwise only the selected year is shown.
        ''' </summary>
        ''' -------------------------------------------------------------------
        Private Sub LoadVectorGrid()

            If (Me.m_cfgRestrictedAreas Is Nothing) Then Return
            If (Me.m_dgvVector Is Nothing) Then Return
            If (Me.m_cbVectorFleet Is Nothing) Then Return
            If (Me.m_cbVectorFleet.SelectedItem Is Nothing) Then Return

            Dim strFleet As String = Me.m_cbVectorFleet.SelectedItem.ToString()
            Dim nZones As Integer = Me.m_cfgRestrictedAreas.Zones.Count
            If (nZones = 0) Then
                Me.m_dgvVector.Rows.Clear()
                Return
            End If

            Dim yrRange As Tuple(Of Integer, Integer) = Me.GetSimulationYearRange()
            Dim firstYear As Integer = yrRange.Item1
            Dim lastYear As Integer = yrRange.Item2

            ' Ensure data exists before reading
            Me.m_cfgRestrictedAreas.EnsureYearRange(firstYear, lastYear, nZones)

            ' Determine year filter
            Dim filterYear As Integer = -1 ' -1 = All
            If (Me.m_cbVectorYear IsNot Nothing AndAlso Me.m_cbVectorYear.SelectedItem IsNot Nothing) Then
                Dim s As String = Me.m_cbVectorYear.SelectedItem.ToString()
                If (s <> c_AllYearsDisplay) Then
                    Integer.TryParse(s, filterYear)
                End If
            End If

            Dim years As New List(Of Integer)
            If (filterYear = -1) Then
                For yr As Integer = firstYear To lastYear
                    years.Add(yr)
                Next
            Else
                years.Add(filterYear)
            End If

            Me.m_bLoadingVector = True
            Try
                Me.m_dgvVector.Rows.Clear()
                For Each yr As Integer In years
                    Dim matrix As Integer()() = Me.m_cfgRestrictedAreas.GetOrCreateVector(strFleet, yr, nZones)
                    For iZone As Integer = 0 To nZones - 1
                        Dim iRow As Integer = Me.m_dgvVector.Rows.Add()
                        Dim row As DataGridViewRow = Me.m_dgvVector.Rows(iRow)
                        row.HeaderCell.Value = String.Format("{0} [{1}]", Me.m_cfgRestrictedAreas.Zones(iZone).Name, yr)
                        row.Tag = Tuple.Create(yr, iZone)
                        For m As Integer = 0 To cRestrictedAreasConfig.nMonths - 1
                            Dim st As eRestrictedAreaStatus = CType(matrix(iZone)(m), eRestrictedAreaStatus)
                            row.Cells(m).Value = StatusDisplay(st)
                        Next
                    Next
                Next
                For Each row As DataGridViewRow In Me.m_dgvVector.Rows
                    row.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleLeft
                Next
            Finally
                Me.m_bLoadingVector = False
            End Try

        End Sub

        ''' -------------------------------------------------------------------
        ''' <summary>
        ''' Store the restriction grid of the currently selected fleet/year view
        ''' back into the configuration and persist it. Row.Tag holds (year, zoneIdx).
        ''' </summary>
        ''' -------------------------------------------------------------------
        Private Sub SaveVectorGrid()

            If (Me.m_bLoadingVector) Then Return
            If (Me.m_cfgRestrictedAreas Is Nothing) Then Return
            If (Me.m_dgvVector Is Nothing) Then Return
            If (Me.m_cbVectorFleet Is Nothing) Then Return
            If (Me.m_cbVectorFleet.SelectedItem Is Nothing) Then Return

            Dim strFleet As String = Me.m_cbVectorFleet.SelectedItem.ToString()
            Dim nZones As Integer = Me.m_cfgRestrictedAreas.Zones.Count

            For Each row As DataGridViewRow In Me.m_dgvVector.Rows
                If (row.Tag Is Nothing) Then Continue For
                Dim tup As Tuple(Of Integer, Integer) = TryCast(row.Tag, Tuple(Of Integer, Integer))
                If (tup Is Nothing) Then Continue For
                Dim yr As Integer = tup.Item1
                Dim zIdx As Integer = tup.Item2
                If (zIdx < 0 OrElse zIdx >= nZones) Then Continue For
                Dim matrix As Integer()() = Me.m_cfgRestrictedAreas.GetOrCreateVector(strFleet, yr, nZones)
                For m As Integer = 0 To cRestrictedAreasConfig.nMonths - 1
                    Dim v As Object = row.Cells(m).Value
                    Dim st As eRestrictedAreaStatus = eRestrictedAreaStatus.Open
                    If (v IsNot Nothing) Then
                        st = StatusFromDisplay(v.ToString())
                    End If
                    matrix(zIdx)(m) = CInt(st)
                Next
            Next

            Me.PersistRestrictedAreas()

        End Sub

        ''' -------------------------------------------------------------------
        ''' <summary>
        ''' Load the restriction grid of the newly selected fleet.
        ''' </summary>
        ''' -------------------------------------------------------------------
        Private Sub OnVectorFleetChanged(sender As Object, e As EventArgs) Handles m_cbVectorFleet.SelectedIndexChanged
            Me.LoadVectorGrid()
        End Sub

        ''' -------------------------------------------------------------------
        ''' <summary>
        ''' Reload when the year filter changes.
        ''' </summary>
        ''' -------------------------------------------------------------------
        Private Sub OnVectorYearChanged(sender As Object, e As EventArgs) Handles m_cbVectorYear.SelectedIndexChanged
            Me.LoadVectorGrid()
        End Sub

        ''' -------------------------------------------------------------------
        ''' <summary>
        ''' Persist the restriction grid whenever the user edits a status.
        ''' </summary>
        ''' -------------------------------------------------------------------
        Private Sub OnVectorCellValueChanged(sender As Object, e As DataGridViewCellEventArgs) _
            Handles m_dgvVector.CellValueChanged
            Me.SaveVectorGrid()
        End Sub

        ''' -------------------------------------------------------------------
        ''' <summary>
        ''' Handle the Import CSV button: let the user pick a sparse interval
        ''' file (flottille;zone;debut;fin;statut / fleet;zone;start;end;status),
        ''' replace the current vector, and report conflicts with line numbers.
        ''' Rows with statut "ouvert"/"open" are silently ignored.
        ''' </summary>
        ''' -------------------------------------------------------------------
        Private Sub OnImportVector(sender As Object, e As EventArgs) Handles m_btnImportVector.Click
            If (Me.m_cfgRestrictedAreas Is Nothing) Then Return
            If (Me.m_cfgRestrictedAreas.Zones.Count = 0) Then
                MessageBox.Show(Me,
                                "Please define at least one zone in the table above before importing a CSV.",
                                "Import restricted areas",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Warning)
                Return
            End If

            Using dlg As New OpenFileDialog()
                dlg.Title = "Import restricted areas (sparse intervals)"
                dlg.Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*"
                dlg.FilterIndex = 1
                dlg.CheckFileExists = True
                dlg.CheckPathExists = True
                dlg.Multiselect = False
                dlg.RestoreDirectory = True
                If (dlg.ShowDialog(Me) <> DialogResult.OK) Then Return

                Dim yrRange As Tuple(Of Integer, Integer) = Me.GetSimulationYearRange()
                Dim firstYear As Integer = yrRange.Item1
                Dim lastYear As Integer = yrRange.Item2

                ' Replace existing vector: clear and re-ensure year range as all-open
                Me.m_cfgRestrictedAreas.Vector.Clear()
                Me.m_cfgRestrictedAreas.EnsureYearRange(firstYear, lastYear, Me.m_cfgRestrictedAreas.Zones.Count)

                Dim err As String = ""
                If (Me.m_cfgRestrictedAreas.ImportSparseCsv(dlg.FileName, err)) Then
                    Me.PersistRestrictedAreas()
                    Me.LoadVectorGrid()
                    MessageBox.Show(Me,
                                    String.Format("Import successful from {0}.", Path.GetFileName(dlg.FileName)),
                                    "Import restricted areas",
                                    MessageBoxButtons.OK,
                                    MessageBoxIcon.Information)
                Else
                    ' Revert to persisted state and reload
                    Me.m_cfgRestrictedAreas = Me.Core.EcospaceModelParameters().GetRestrictedAreasConfig()
                    Me.LoadVectorGrid()
                    MessageBox.Show(Me,
                                    err,
                                    "Import failed",
                                    MessageBoxButtons.OK,
                                    MessageBoxIcon.Error)
                End If
            End Using
        End Sub

        ''' -------------------------------------------------------------------
        ''' <summary>
        ''' Read the F_&lt;count&gt;.csv file exported by FIBE and store the fishing
        ''' mortality per fleet,group,row,col in <see cref="cEcospaceDataStructures.FtotFIBE"/>.
        ''' </summary>
        ''' <param name="count">FIBE step count for the current month.</param>
        ''' <param name="basePath">Folder that contains the FIBE results.</param>
        ''' -------------------------------------------------------------------
        Private Sub LoadFIBEFishingMortality(count As Integer, basePath As String)

            Dim ds As cEcospaceDataStructures = Me.Core.EcospaceDataStructures
            If (ds Is Nothing) Then Return
            If (ds.FtotFIBE Is Nothing) Then Return

            Dim fileName As String = "F_" & count.ToString & ".csv"
            Dim fullPath As String = System.IO.Path.Combine(basePath, fileName)

            ' FIBE writes the F file at the same time as the agent file, but wait
            ' a bounded amount of time to be defensive (no infinite block)
            Dim nWaited As Integer = 0
            While (Not System.IO.File.Exists(fullPath)) And (nWaited < 30)
                System.Threading.Thread.Sleep(1000)
                nWaited += 1
            End While
            If Not System.IO.File.Exists(fullPath) Then
                Me.m_logger.LogWarning("FIBE coupling: F file not found {File}, continuing without FIBE fishing mortality for this month", fullPath)
                Return
            End If

            ' Clear the previous month values, the file may only contain part of the cells
            Array.Clear(ds.FtotFIBE, 0, ds.FtotFIBE.Length)

            Dim nFleets As Integer = ds.nFleets
            Dim nGroups As Integer = ds.NGroups
            Dim nRows As Integer = ds.InRow
            Dim nCols As Integer = ds.InCol
            Dim nLoaded As Integer = 0

            For Each strLine As String In System.IO.File.ReadAllLines(fullPath)
                If String.IsNullOrWhiteSpace(strLine) Then Continue For
                If strLine.StartsWith("row") OrElse strLine.StartsWith("ligne") Then Continue For ' header
                Dim parts() As String = strLine.Split(New Char() {";"c, ","c}, StringSplitOptions.None)
                If parts.Length < 5 Then Continue For

                Dim iRow As Integer = 0
                Dim iCol As Integer = 0
                Dim iFlt As Integer = 0
                Dim iGrp As Integer = 0
                Dim fVal As Single = 0

                If Not Integer.TryParse(parts(0), iRow) Then Continue For
                If Not Integer.TryParse(parts(1), iCol) Then Continue For
                If Not Integer.TryParse(parts(2), iFlt) Then Continue For
                If Not Integer.TryParse(parts(3), iGrp) Then Continue For
                If Not Single.TryParse(parts(4), NumberStyles.Float, CultureInfo.InvariantCulture, fVal) Then Continue For

                ' Bounds check, the Ecospace arrays use 1-based indexes
                If (iRow < 1) Or (iRow > nRows) Then Continue For
                If (iCol < 1) Or (iCol > nCols) Then Continue For
                If (iFlt < 1) Or (iFlt > nFleets) Then Continue For
                If (iGrp < 1) Or (iGrp > nGroups) Then Continue For

                ds.FtotFIBE(iFlt, iGrp, iRow, iCol) = fVal
                nLoaded += 1
            Next

            Me.m_logger.LogInformation("FIBE coupling: loaded {N} fishing mortality values from {File}", nLoaded, fullPath)

        End Sub

        Private Sub OnEcospaceTimeStep(ByRef ts As cEcospaceTimestep)

            If (ts Is Nothing) Then Return
            If (Not Me.m_Couplage.Checked) Then Return

            Dim map As Single(,,) = ts.BiomassMap()
            Dim baseDir As String = AppDomain.CurrentDomain.BaseDirectory
            Dim targetFolder As String = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "..", "..", "Couplage", "Data"))
            Dim targetBiomassFolder As String = Path.GetFullPath(Path.Combine(targetFolder, "Biomass"))
            If Not Directory.Exists(targetFolder) Then
                Directory.CreateDirectory(targetFolder)
            End If
            If Not Directory.Exists(targetBiomassFolder) Then
                Directory.CreateDirectory(targetBiomassFolder)
            End If
            Dim fileName As String = Path.Combine(targetBiomassFolder, "EcospaceBiomassMap.txt")

            If ts.iTimeStep = 1 Then
                Me.SaveStaticMaps(targetFolder)
                Me.ExportRestrictedAreas(targetFolder)
                Me.ExportFibeAgents(targetFolder)
            End If
            Me.ExportAgentNumbers(targetFolder)
            Me.SaveOffVesselPriceToTxt(ts, targetFolder)
            Me.SaveLandingsToTxt(ts, targetFolder)
            Me.SaveBiomassMapToTxt(map, fileName, ts)


        End Sub

        Private Sub SaveBiomassMapToTxt(map As Single(,,), fileName As String, ByRef ts As cEcospaceTimestep)

            Dim sb As New System.Text.StringBuilder()
            sb.AppendLine("row;col;group;biomass")

            Dim groupFirst As Integer = map.GetLowerBound(2)
            Dim groupLast As Integer = map.GetUpperBound(2)

            For row As Integer = 1 To Me.Core.EcospaceDataStructures.InRow
                For col As Integer = 1 To Me.Core.EcospaceDataStructures.InCol
                    For group As Integer = groupFirst To groupLast
                        sb.AppendFormat("{0};{1};{2};{3}", row, col, group, map(row, col, group))
                        sb.AppendLine()
                    Next
                Next
            Next

            Dim tmpFile As String = fileName & ".tmp"
            System.IO.File.WriteAllText(tmpFile, sb.ToString())
            If System.IO.File.Exists(fileName) Then
                System.IO.File.Replace(tmpFile, fileName, Nothing)
            Else
                System.IO.File.Move(tmpFile, fileName)
            End If

            Dim timeStep As Integer = ts.iTimeStep

            If timeStep = 1 Then
                Dim installScriptPath As String = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..", "..", "Couplage", "install_FIBE.ps1")
                Me.RunInstallScript(installScriptPath)
            End If

            Dim count As Integer = timeStep * 28 - 28

            Dim basePath As String = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..", "..", "Couplage", "FIBE", "results", "biomass")
            Dim targetFileName As String = $"agent_{count}.csv"
            Dim fullPath As String = System.IO.Path.Combine(basePath, targetFileName)
            Dim runTime As TextBox = Me.m_tbTotalTime
            Dim valueRunTime As Integer = CInt(runTime.Text.Replace("""", ""))
            Dim iFirstYear As Integer = Me.Core.EcosimFirstYear()
            If iFirstYear <= 0 Then iFirstYear = 2000

            If System.IO.Directory.Exists(basePath) Then
                Debug.WriteLine($"Contenu de {basePath} :")
                For Each f As String In System.IO.Directory.GetFiles(basePath)
                    Debug.WriteLine(System.IO.Path.GetFileName(f))
                Next
            End If

            ' Réécrire config.json (post-save) AVANT d'attendre le fichier agent
            ' exporté par FIBE : c'est cette réécriture qui débloque le wait de
            ' FIBE (mtime de config.json). Si on attend l'agent d'abord, on crée
            ' un deadlock (Ecospace attend agent_{count}.csv que FIBE ne peut
            ' écrire qu'après avoir reçu le nouveau config.json).
            Dim bm As cEcospaceBasemap = Me.Core.EcospaceBasemap
            Dim dWestLon As Single = bm.Longitude
            Dim dNorthLat As Single = bm.Latitude
            Dim dCellSize As Single = bm.CellSize
            Me.RunPostSaveScript(fileName, ts.iTimeStep, valueRunTime, iFirstYear, dWestLon, dNorthLat, dCellSize)

            If timeStep > 1 Then
                Debug.WriteLine($"Waiting for file: {targetFileName}")

                Dim waitStart As DateTime = DateTime.UtcNow
                Dim waitTimeout As TimeSpan = TimeSpan.FromMinutes(5) ' 5 minutes timeout

                While Not System.IO.File.Exists(fullPath)
                    If DateTime.UtcNow - waitStart > waitTimeout Then
                        Dim msg As String = String.Format(
                            "FIBE coupling: timeout waiting for {0}" &
                            " after {1} minutes. Continuing without" &
                            " fishing mortality update.",
                            targetFileName, waitTimeout.TotalMinutes
                            )
                        Me.WriteFibeLog("ERROR: " & msg)
                        m_logger.LogError(msg)
                        Exit While
                    End If
                    System.Threading.Thread.Sleep(2000) ' Pause de 2 secondes
                End While

                If System.IO.File.Exists(fullPath) Then
                    Debug.WriteLine($"File found: {targetFileName}")

                    ' FIBE coupling: read the fishing mortality exported by FIBE for this month
                    ' (F_<count>.csv is written by FIBE at the same time as the agent file)
                    Me.LoadFIBEFishingMortality(count, basePath)
                End If
            End If

        End Sub

        Private Sub SaveOffVesselPriceToTxt(ByRef ts As cEcospaceTimestep, targetFolder As String)

            Dim offVesselPrice As Single(,) = ts.OffVesselPrice
            'If offVesselPrice Is Nothing Then Return

            Dim targetOffVesselPriceFolder As String = Path.GetFullPath(Path.Combine(targetFolder, "OffVesselPrice"))
            If Not Directory.Exists(targetOffVesselPriceFolder) Then
                Directory.CreateDirectory(targetOffVesselPriceFolder)
            End If

            Dim fileName As String = Path.Combine(targetOffVesselPriceFolder, "EcospaceOffVesselPrice.txt")

            Dim sb As New System.Text.StringBuilder()
            sb.AppendLine("fleet;group;off_vessel_price")

            Dim fleetFirst As Integer = offVesselPrice.GetLowerBound(0)
            Dim fleetLast As Integer = offVesselPrice.GetUpperBound(0)
            Dim groupFirst As Integer = offVesselPrice.GetLowerBound(1)
            Dim groupLast As Integer = offVesselPrice.GetUpperBound(1)

            For fleet As Integer = fleetFirst To fleetLast
                For group As Integer = groupFirst To groupLast
                    sb.AppendFormat(System.Globalization.CultureInfo.InvariantCulture, "{0};{1};{2}", fleet, group, offVesselPrice(fleet, group))
                    sb.AppendLine()
                Next
            Next

            System.IO.File.WriteAllText(fileName, sb.ToString())

        End Sub

        Private Sub SaveLandingsToTxt(ByRef ts As cEcospaceTimestep, targetFolder As String)

            Dim nFleets As Integer = Me.Core.nFleets
            Dim nGroups As Integer = Me.Core.nGroups

            Dim landings As Single(,) = New Single(nFleets, nGroups) {}

            For fleet As Integer = 1 To nFleets
                For group As Integer = 1 To nGroups
                    landings(fleet, group) = Me.Core.EcopathFleetInputs(fleet).Landings(group)
                Next
            Next

            Dim targetLandingsFolder As String = Path.GetFullPath(Path.Combine(targetFolder, "Landings"))
            If Not Directory.Exists(targetLandingsFolder) Then
                Directory.CreateDirectory(targetLandingsFolder)
            End If

            Dim fileName As String = Path.Combine(targetLandingsFolder, "EcospaceLandings.txt")

            Dim sb As New System.Text.StringBuilder()
            sb.AppendLine("fleet;group;landings")

            Dim fleetFirst As Integer = landings.GetLowerBound(0)
            Dim fleetLast As Integer = landings.GetUpperBound(0)
            Dim groupFirst As Integer = landings.GetLowerBound(1)
            Dim groupLast As Integer = landings.GetUpperBound(1)

            For fleet As Integer = fleetFirst To fleetLast
                For group As Integer = groupFirst To groupLast
                    sb.AppendFormat(System.Globalization.CultureInfo.InvariantCulture, "{0};{1};{2}", fleet, group, landings(fleet, group))
                    sb.AppendLine()
                Next
            Next

            System.IO.File.WriteAllText(fileName, sb.ToString())

        End Sub

        Private Sub SaveStaticMaps(targetFolder As String)

            Dim bm As cEcospaceBasemap = Me.Core.EcospaceBasemap

            Try
                Dim targetFolderDepth As String = Path.GetFullPath(Path.Combine(targetFolder, "Depth"))
                If Not Directory.Exists(targetFolderDepth) Then
                    Directory.CreateDirectory(targetFolderDepth)
                End If
                Dim depth As cEcospaceLayerDepth = bm.LayerDepth
                Dim sbDepth As New System.Text.StringBuilder()
                sbDepth.AppendLine("row;col;depth")
                For r As Integer = 1 To bm.InRow
                    For c As Integer = 1 To bm.InCol
                        If bm.IsModelledCell(r, c) Then
                            Dim v As Single = CSng(depth.Cell(r, c))
                            sbDepth.AppendFormat(System.Globalization.CultureInfo.InvariantCulture, "{0};{1};{2}", r, c, v)
                            sbDepth.AppendLine()
                        End If
                    Next
                Next
                File.WriteAllText(Path.Combine(targetFolderDepth, "DepthMap.txt"), sbDepth.ToString())
            Catch ex As Exception
                Debug.WriteLine("SaveStaticMaps Depth error: " & ex.Message)
            End Try

            Try
                Dim targetFolderPorts As String = Path.GetFullPath(Path.Combine(targetFolder, "Ports"))
                If Not Directory.Exists(targetFolderPorts) Then
                    Directory.CreateDirectory(targetFolderPorts)
                End If
                Dim sbPorts As New System.Text.StringBuilder()
                sbPorts.AppendLine("row;col;port")
                Dim portData As Boolean()(,) = Me.Core.EcospaceDataStructures.Port
                Dim nPortCells As Integer = 0
                For r As Integer = 1 To bm.InRow
                    For c As Integer = 1 To bm.InCol
                        Dim hasPort As Boolean = False
                        For iFleet As Integer = 0 To Me.Core.EcospaceDataStructures.nFleets
                            If portData(iFleet) IsNot Nothing AndAlso portData(iFleet)(r, c) Then
                                hasPort = True
                                Exit For
                            End If
                        Next
                        If hasPort Then nPortCells += 1
                        sbPorts.AppendFormat(System.Globalization.CultureInfo.InvariantCulture, "{0};{1};{2}", r, c, If(hasPort, 1, 0))
                        sbPorts.AppendLine()
                    Next
                Next
                Debug.WriteLine("SaveStaticMaps Ports: " & nPortCells & " port cells exported (" & Me.Core.nFleets & " fleets)")
                Dim sbDbg As New System.Text.StringBuilder()
                sbDbg.Append(String.Format(System.Globalization.CultureInfo.InvariantCulture,
                    "EXPORT target={0} InRow={1} InCol={2} nFleets={3} allCells={4} total={5}",
                    targetFolderPorts, bm.InRow, bm.InCol, Me.Core.nFleets, bm.InRow * bm.InCol, nPortCells))
                For iFleet2 As Integer = 0 To Me.Core.EcospaceDataStructures.nFleets
                    Dim cnt As Integer = 0
                    If portData(iFleet2) IsNot Nothing Then
                        For rr As Integer = 1 To bm.InRow
                            For cc As Integer = 1 To bm.InCol
                                If portData(iFleet2)(rr, cc) Then cnt += 1
                            Next
                        Next
                    End If
                    sbDbg.Append(String.Format(System.Globalization.CultureInfo.InvariantCulture, " | f{0}={1}", iFleet2, cnt))
                Next
                Try
                    System.IO.File.AppendAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ewe_ports_debug.log"),
                        sbDbg.ToString() & Environment.NewLine)
                Catch
                End Try
                File.WriteAllText(Path.Combine(targetFolderPorts, "PortsMap.txt"), sbPorts.ToString())
            Catch ex As Exception
                Debug.WriteLine("SaveStaticMaps Ports error: " & ex.Message)
            End Try

            Try
                Dim targetFolderHabitats As String = Path.GetFullPath(Path.Combine(targetFolder, "Habitats"))
                If Not Directory.Exists(targetFolderHabitats) Then
                    Directory.CreateDirectory(targetFolderHabitats)
                End If

                ' Nettoyer les anciennes couches d'habitat : si le scénario change
                ' de basemap ou de nombre de couches, les fichiers restés d'un run
                ' précédent (grille différente) font planter FIBE (np.stack : les
                ' formes ne correspondent pas).
                For Each stale As String In Directory.GetFiles(targetFolderHabitats, "Habitat_*")
                    Try
                        File.Delete(stale)
                        m_logger.LogInformation("Deleted stale habitat file {File}", stale)
                    Catch ex As Exception
                        m_logger.LogWarning("Failed to delete stale habitat file {File}: {Message}", stale, ex.Message)
                    End Try
                Next

                Dim noHab As Integer = Math.Max(0, Me.Core.EcospaceDataStructures.NoHabitats - 1)
                For ih As Integer = 1 To noHab
                    Dim hab As cEcospaceLayerHabitat = bm.LayerHabitat(ih)
                    Dim habName As String = ""
                    Try
                        habName = Me.Core.EcospaceHabitats(ih).Name
                    Catch
                        habName = "Hab" & ih
                    End Try
                    Dim safeName As String = String.Join("_", habName.Split(Path.GetInvalidFileNameChars()))

                    Using w As New StreamWriter(Path.Combine(targetFolderHabitats, "Habitat_" & ih & "_" & safeName & ".txt"), False)
                        w.WriteLine("row;col;value")
                        For r As Integer = 1 To bm.InRow
                            For c As Integer = 1 To bm.InCol
                                If bm.IsModelledCell(r, c) Then
                                    Dim v As Single = CSng(hab.Cell(r, c))
                                    w.WriteLine(String.Format(CultureInfo.InvariantCulture, "{0};{1};{2}", r, c, v))
                                End If
                            Next
                        Next
                    End Using
                Next
            Catch ex As Exception
                Debug.WriteLine("SaveStaticMaps Habitats error: " & ex.Message)
            End Try

        End Sub

        ''' -------------------------------------------------------------------
        ''' <summary>
        ''' Export the restricted areas configuration for the FIBE coupling:
        ''' <list type="bullet">
        ''' <item><description>the per-fleet, per-year restriction intervals as
        ''' a sparse CSV (flottille;zone;debut;fin;statut) readable by diatome;
        ''' only non-open intervals are written, inclusive, MM/YYYY;</description></item>
        ''' <item><description>a JSON file ("restricted_zones.json") with the
        ''' "restricted_area_map" and "restricted_area_vector" entries that
        ''' CreateJSON.ps1 merges into the diatome config.</description></item>
        ''' </list>
        ''' </summary>
        ''' <param name="targetFolder">Coupling data folder (Couplage/Data).</param>
        ''' -------------------------------------------------------------------
        Private Sub ExportRestrictedAreas(targetFolder As String)

            If (Me.Core Is Nothing) Then Return

            Dim cfg As cRestrictedAreasConfig = Me.Core.EcospaceModelParameters().GetRestrictedAreasConfig()
            If (cfg.Zones.Count = 0) Then
                m_logger.LogInformation("FIBE coupling: no restricted zones configured, skipping restricted area export")
                Return
            End If

            Try
                ' --- 1. Sparse interval CSV (per-fleet, per-year) -----------
                Dim vecFolder As String = Path.GetFullPath(Path.Combine(targetFolder, "RestrictedArea"))
                If Not Directory.Exists(vecFolder) Then
                    Directory.CreateDirectory(vecFolder)
                End If
                Dim vecPath As String = Path.Combine(vecFolder, "restricted_area_vector.csv")

                Dim yrRange As Tuple(Of Integer, Integer) = Me.GetSimulationYearRange()
                Dim firstYear As Integer = yrRange.Item1
                Dim lastYear As Integer = yrRange.Item2

                ' Ensure matrices exist for the full simulation horizon before export
                cfg.EnsureYearRange(firstYear, lastYear, cfg.Zones.Count)
                cfg.ExportSparseCsv(vecPath, firstYear, lastYear)
                m_logger.LogInformation("FIBE coupling: restricted area vector exported to {Path}", vecPath)

                ' --- 2. JSON fragment for CreateJSON.ps1 ---------------------
                Dim json As New Newtonsoft.Json.Linq.JObject()
                Dim jsonZones As New Newtonsoft.Json.Linq.JObject()
                For Each zone As cRestrictedAreaZone In cfg.Zones
                    If (String.IsNullOrWhiteSpace(zone.Name)) Then Continue For
                    jsonZones(zone.Name) = zone.ShapefilePath
                Next
                json("restricted_area_map") = jsonZones
                json("restricted_area_vector") = vecPath

                Dim jsonPath As String = Path.Combine(targetFolder, "restricted_zones.json")
                System.IO.File.WriteAllText(jsonPath, json.ToString(Newtonsoft.Json.Formatting.None))
                m_logger.LogInformation("FIBE coupling: restricted area configuration exported to {Path}", jsonPath)
            Catch ex As Exception
                m_logger.LogError(ex, "FIBE coupling: failed to export restricted areas")
            End Try

        End Sub

        ''' -------------------------------------------------------------------
        ''' <summary>
        ''' Export the initial FIBE agents (Agents tab) for the coupling.
        ''' Writes an aggregated per-fleet JSON file ("fibe_agents.json")
        ''' that CreateJSON.ps1 merges into config.json at step 1 only.
        ''' An empty grid exports nothing (FIBE keeps config_default.json).
        ''' </summary>
        ''' <param name="targetFolder">Coupling data folder (Couplage/Data).</param>
        ''' -------------------------------------------------------------------
        Private Sub ExportFibeAgents(targetFolder As String)

            If (Me.Core Is Nothing) Then Return

            Dim cfg As cFibeAgentsConfig = Nothing
            Try
                cfg = Me.Core.EcospaceModelParameters().GetFibeAgentsConfig()
            Catch ex As Exception
                m_logger.LogWarning("FIBE coupling: could not read initial agents configuration: {Message}", ex.Message)
                Return
            End Try
            If (cfg Is Nothing OrElse cfg.Agents.Count = 0) Then
                m_logger.LogInformation("FIBE coupling: no initial agents configured, skipping fibe agents export (config_default.json kept)")
                Return
            End If

            Try
                Dim json As Newtonsoft.Json.Linq.JObject = cfg.ToFibeJson()
                Dim counts As Dictionary(Of String, Integer) = cfg.GetCounts()

                ' Soft validation: the FIBE ports map is only known at run time,
                ' so a suspicious port index only triggers a warning here.
                For Each a As cFibeAgent In cfg.Agents
                    If (a.PortNumber < 0) Then
                        m_logger.LogWarning("FIBE coupling: agent '{Name}' has a negative port index {Port}", a.Name, a.PortNumber)
                    End If
                Next

                Dim jsonPath As String = Path.Combine(targetFolder, "fibe_agents.json")
                Dim tmpPath As String = jsonPath & ".tmp"
                System.IO.File.WriteAllText(tmpPath, json.ToString(Newtonsoft.Json.Formatting.None))
                If System.IO.File.Exists(jsonPath) Then
                    System.IO.File.Replace(tmpPath, jsonPath, Nothing)
                Else
                    System.IO.File.Move(tmpPath, jsonPath)
                End If
                m_logger.LogInformation(
                    "FIBE coupling: initial agents exported to {Path} (archipelago={A}, coastal={C}, trawler={T})",
                    jsonPath, counts("archipelago"), counts("coastal"), counts("trawler"))
            Catch ex As Exception
                m_logger.LogError(ex, "FIBE coupling: failed to export initial agents")
            End Try

        End Sub

        Private Sub ExportAgentNumbers(targetFolder As String)
            Dim strPath As String = ""
            Try : strPath = Me.Core.EcospaceModelParameters.AgentNumbersFile : Catch : End Try
            If String.IsNullOrWhiteSpace(strPath) OrElse Not File.Exists(strPath) Then Return
            Try
                Dim json As New Newtonsoft.Json.Linq.JObject()
                json("agent_numbers_file") = strPath
                Dim jsonPath As String = Path.Combine(targetFolder, "agent_numbers.json")
                File.WriteAllText(jsonPath, json.ToString(Newtonsoft.Json.Formatting.None))
                m_logger.LogInformation("FIBE coupling: agent numbers file exported to {Path}", jsonPath)
            Catch ex As Exception
                m_logger.LogError(ex, "FIBE coupling: failed to export agent numbers")
            End Try
        End Sub

        Private Function GetInstallScriptContent() As String
            Dim sb As New System.Text.StringBuilder()
            sb.AppendLine("$scriptDir = $PSScriptRoot")
            sb.AppendLine("$scriptDirParent = (Get-Item $scriptDir).Parent.Parent.FullName")
            sb.AppendLine("$fibePath = Join-Path $scriptDirParent ""FIBE""")
            sb.AppendLine("")
            sb.AppendLine("if (Test-Path $fibePath) {")
            sb.AppendLine("    exit")
            sb.AppendLine("} else {")
            sb.AppendLine("    $fibeParent = Join-Path $scriptDirParent ""FIBE""")
            sb.AppendLine("    if (-not (Test-Path $fibeParent)) {")
            sb.AppendLine("        New-Item -Path $fibeParent -ItemType Directory -Force | Out-Null")
            sb.AppendLine("    }")
            sb.AppendLine("    Set-Location $fibeParent")
            sb.AppendLine("    git clone https://github.com/enzochoffat/diatome.git")
            sb.AppendLine("    Set-Location $fibePath")
            sb.AppendLine("    python -m venv venv")
            sb.AppendLine("    .\venv\Scripts\Activate.ps1")
            sb.AppendLine("    pip install -r requirement_coupling.txt")
            sb.AppendLine("}")
            Return sb.ToString()
        End Function

        Private Sub RunInstallScript(fileName As String)

            Dim scriptDir As String = Path.GetDirectoryName(fileName)
            Dim scriptInstallPath As String = Path.Combine(scriptDir, "install_FIBE.ps1")

            If Not File.Exists(scriptInstallPath) Then
                Dim content As String = Me.GetInstallScriptContent()
                File.WriteAllText(scriptInstallPath, content)
                m_logger.LogInformation("Install script created at {Path}", scriptInstallPath)
            Else
                m_logger.LogInformation("Install script already exists at {Path}", scriptInstallPath)
            End If

            Dim psi As New ProcessStartInfo()
            psi.FileName = "powershell.exe"
            psi.Arguments = String.Format("-NoExit -ExecutionPolicy Bypass -File ""{0}""", scriptInstallPath)
            psi.UseShellExecute = True
            psi.CreateNoWindow = True
            psi.WorkingDirectory = Path.GetDirectoryName(scriptInstallPath)

            Dim p As Process = Process.Start(psi)
            p.WaitForExit()
            Dim exitCode As Integer = p.ExitCode
            If exitCode = 0 Then
                m_logger.LogInformation("Install script exited with code 0")
            Else
                m_logger.LogError("Install script exited with code {Code}", exitCode)
            End If

        End Sub

        Private Function GetPostSaveScriptContent(westLon As Single, northLat As Single, cellSize As Single) As String
            Dim sb As New System.Text.StringBuilder()
            Dim inv As System.Globalization.CultureInfo = System.Globalization.CultureInfo.InvariantCulture
            Dim sWest As String = westLon.ToString(inv)
            Dim sNorth As String = northLat.ToString(inv)
            Dim sCellSize As String = cellSize.ToString(inv)
            sb.AppendLine("param(")
            sb.AppendLine("    [Parameter(Mandatory = $true)]")
            sb.AppendLine("    [string]$InputFile,")
            sb.AppendLine("    [Parameter(Mandatory = $true)]")
            sb.AppendLine("    [int]$TimeStep,")
            sb.AppendLine("    [Parameter(Mandatory = $true)]")
            sb.AppendLine("    $runTime,")
            sb.AppendLine("    [Parameter(Mandatory = $true)]")
            sb.AppendLine("    [int]$FirstYear,")
            sb.AppendLine("    [Parameter(Mandatory = $true)]")
            sb.AppendLine("    [string]$WestLon,")
            sb.AppendLine("    [Parameter(Mandatory = $true)]")
            sb.AppendLine("    [string]$NorthLat,")
            sb.AppendLine("    [Parameter(Mandatory = $true)]")
            sb.AppendLine("    [string]$CellSize,")
            sb.AppendLine("    [Parameter(Mandatory = $true)]")
            sb.AppendLine("    [string]$LogFile")
            sb.AppendLine(")")
            sb.AppendLine("")
            sb.AppendLine("New-Item -Path (Split-Path $LogFile) -ItemType Directory -Force | Out-Null")
            sb.AppendLine("")
            sb.AppendLine("function Run-Python([string]$Script, [string]$Arg) {")
            sb.AppendLine("    $venvPython = Join-Path (Split-Path (Split-Path $scriptDir -Parent) -Parent) ""FIBE\venv\Scripts\python.exe""")
            sb.AppendLine("    if (Test-Path $venvPython) { $py = $venvPython } else { $py = ""python"" }")
            sb.AppendLine("    try {")
            sb.AppendLine("        $out = & $py $Script $Arg 2>&1 | Out-String")
            sb.AppendLine("        $code = $LASTEXITCODE")
            sb.AppendLine("    } catch {")
            sb.AppendLine("        $out = ""EXCEPTION: "" + $_.Exception.Message")
            sb.AppendLine("        $code = 1")
            sb.AppendLine("    }")
            sb.AppendLine("    $out | Out-File -FilePath $LogFile -Append -Encoding utf8")
            sb.AppendLine("    if ($code -ne 0) {")
            sb.AppendLine("        (""ERROR: "" + $Script + "" exited with code "" + $code) | Out-File -FilePath $LogFile -Append -Encoding utf8")
            sb.AppendLine("        exit $code")
            sb.AppendLine("    }")
            sb.AppendLine("}")
            sb.AppendLine("")
            sb.AppendLine("$scriptDir = $PSScriptRoot")
            sb.AppendLine("New-Item -Path (Split-Path $scriptDir) -Name ""Biomass"" -ItemType Directory -Force")
            sb.AppendLine("$filePath = Join-Path $scriptDir $InputFile")
            sb.AppendLine("")
            sb.AppendLine("$parentDir = Split-Path (Split-Path $scriptDir -Parent) -Parent")

            ' Script Python principal
            sb.AppendLine("$pythonScript = Join-Path $parentDir ""FIBE.py""")
            sb.AppendLine("Run-Python $pythonScript $InputFile")

            ' Conversion des maps statiques
            sb.AppendLine("$parentDir = Split-Path (Split-Path $scriptDir -Parent ) -Parent ")
            sb.AppendLine("$pythonScript = Join-Path $parentDir ""Convert_static_map.py""")

            sb.AppendLine("$InputFile = Join-Path (Split-Path $scriptDir -Parent) ""Depth""")
            sb.AppendLine("Run-Python $pythonScript $InputFile")
            sb.AppendLine("")

            sb.AppendLine("$InputFile = Join-Path (Split-Path $scriptDir -Parent) ""Ports""")
            sb.AppendLine("Run-Python $pythonScript $InputFile")
            sb.AppendLine("")

            sb.AppendLine("$InputFile = Join-Path (Split-Path $scriptDir -Parent) ""Habitats""")
            sb.AppendLine("Run-Python $pythonScript $InputFile")
            sb.AppendLine("")

            ' Conversion off vessel price
            sb.AppendLine("$pythonScript = Join-Path $parentDir ""Convert_off_vessel_price.py""")
            sb.AppendLine("$InputFile = Join-Path (Split-Path $scriptDir -Parent) ""OffVesselPrice""")
            sb.AppendLine("Run-Python $pythonScript $InputFile")
            sb.AppendLine("")

            ' Conversion landings
            sb.AppendLine("$pythonScript = Join-Path $parentDir ""Convert_landings.py""")
            sb.AppendLine("$InputFile = Join-Path (Split-Path $scriptDir -Parent) ""Landings""")
            sb.AppendLine("Run-Python $pythonScript $InputFile")
            sb.AppendLine("")

            sb.AppendLine("$createJsonPath = Join-Path (Split-Path (Split-Path $scriptDir -Parent) -Parent) ""CreateJSON.ps1""")
            sb.AppendLine("try {")
            sb.AppendLine("    $out = & $createJsonPath $TimeStep $runTime $FirstYear " & sWest & " " & sNorth & " " & sCellSize & " 2>&1 | Out-String")
            sb.AppendLine("    $code = $LASTEXITCODE")
            sb.AppendLine("    $out | Out-File -FilePath $LogFile -Append -Encoding utf8")
            sb.AppendLine("    if ($code -ne 0) {")
            sb.AppendLine("        (""ERROR: CreateJSON.ps1 exited with code "" + $code) | Out-File -FilePath $LogFile -Append -Encoding utf8")
            sb.AppendLine("        exit $code")
            sb.AppendLine("    }")
            sb.AppendLine("} catch {")
            sb.AppendLine("    $out = ""EXCEPTION: "" + $_.Exception.Message")
            sb.AppendLine("    $out | Out-File -FilePath $LogFile -Append -Encoding utf8")
            sb.AppendLine("    exit 1")
            sb.AppendLine("}")

            Return sb.ToString()
        End Function

        Private Sub RunPostSaveScript(fileName As String, timeStep As Integer, valueRunTime As Integer, iFirstYear As Integer, westLon As Single, northLat As Single, cellSize As Single)

            Dim scriptDir As String = Path.GetDirectoryName(fileName)
            Dim scriptPath As String = Path.Combine(scriptDir, "post_save.ps1")
            Dim logFilePath As String = Path.Combine(Path.GetDirectoryName(LoggingContext.LogFile), $"couplage-{DateTime.Now:yyyyMMdd}.log")

            ' Toujours régénérer le script : il doit rester synchronisé avec le
            ' code (ajout/retrait d'étapes de conversion).
            Dim content As String = Me.GetPostSaveScriptContent(westLon, northLat, cellSize)
            File.WriteAllText(scriptPath, content)
            m_logger.LogInformation("Post save script created at {Path}", scriptPath)
            Dim inv As System.Globalization.CultureInfo = System.Globalization.CultureInfo.InvariantCulture

            Dim psi As New ProcessStartInfo()
            psi.FileName = "powershell.exe"
            ' westLon/northLat/cellSize sont injectés directement dans le script
            ' généré (appel CreateJSON.ps1), ils ne doivent PAS être passés en
            ' arguments positionnels : le script n'a que 5 paramètres et
            ' PowerShell échouerait à la liaison avant même de créer le log.
            psi.Arguments = String.Format("-ExecutionPolicy Bypass -File ""{0}"" ""{1}"" {2} {3} {4} {5} {6} {7} ""{8}""", scriptPath, fileName, timeStep, valueRunTime, iFirstYear, westLon, northLat, cellSize, logFilePath)
            psi.UseShellExecute = False
            psi.CreateNoWindow = True
            psi.WorkingDirectory = Path.GetDirectoryName(scriptPath)

            ' Attendre la fin du post-save : les conversions (FIBE.py,
            ' CreateJSON.ps1) doivent être terminées avant qu'Ecospace n'avance
            ' d'un pas, sinon deux conversions peuvent écrire les mêmes CSV en
            ' parallèle et FIBE peut lire des fichiers incomplets.
            Dim p As Process = Process.Start(psi)
            p.WaitForExit()
            If p.ExitCode = 0 Then
                m_logger.LogInformation("Post save script exited with code 0")
            Else
                m_logger.LogError("Post save script exited with code {Code}. Details: {LogFile}", p.ExitCode, logFilePath)
            End If

            If timeStep = 1 Then
                Me.StartFIBESimulation()
            End If

        End Sub

        Private Sub StartFIBESimulation()

            Dim baseDir As String = AppDomain.CurrentDomain.BaseDirectory
            Dim diatomePath As String = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "..", "..", "Couplage", "FIBE"))
            Dim venvPython As String = Path.Combine(diatomePath, "venv", "Scripts", "python.exe")
            Dim configPath As String = Path.Combine(diatomePath, "configs_json", "config.json")

            m_logger.LogInformation("Starting FIBE simulation: {Python} -m src.scripts.run_simulation {Config}", venvPython, configPath)

            If Not File.Exists(venvPython) Then
                Me.WriteFibeLog("ERROR: FIBE venv python not found: " & venvPython)
                m_logger.LogError("FIBE venv python not found: {Python}", venvPython)
                Return
            End If

            ' Lancement détaché : la simulation ne doit pas s'exécuter dans le
            ' script post-save (qui doit se terminer rapidement pour ne pas
            ' bloquer les conversions suivantes).
            Dim psi As New ProcessStartInfo()
            psi.FileName = venvPython
            psi.Arguments = String.Format("-m src.scripts.run_simulation ""{0}""", configPath)
            psi.WorkingDirectory = diatomePath
            psi.UseShellExecute = False
            psi.CreateNoWindow = True

            ' Rediriger stdout/stderr : sans cela, le traceback Python est
            ' perdu. Les lignes sont écrites dans Logs\fibe-YYYYMMDD.log.
            psi.RedirectStandardOutput = True
            psi.RedirectStandardError = True
            psi.StandardOutputEncoding = System.Text.Encoding.UTF8
            psi.StandardErrorEncoding = System.Text.Encoding.UTF8
            
            Dim biomassOutDir As String = Path.Combine(diatomePath, "results", "biomass")
            If Directory.Exists(biomassOutDir) Then
                For Each stale As String in Directory.GetFiles(biomassOutDir, "*.csv")
                    Try
                        File.Delete(stale)
                        m_logger.LogInformation("Deleted stale biomass file {File}", stale)
                    Catch ex As IOException
                        m_logger.LogWarning(ex, "Failed to delete stale biomass file {File}", stale)
                    End Try
                Next
            End If

            Dim p As Process = Nothing
            Try
                p = Process.Start(psi)
            Catch ex As Exception
                Me.WriteFibeLog("ERROR: failed to start FIBE process: " & ex.Message)
                m_logger.LogError(ex, "Failed to start FIBE process")
                Return
            End Try
            p.EnableRaisingEvents = True

            AddHandler p.OutputDataReceived, AddressOf Me.OnFibeStdOut
            AddHandler p.ErrorDataReceived, AddressOf Me.OnFibeStdErr
            p.BeginOutputReadLine()
            p.BeginErrorReadLine()

            ' Ne pas attendre la fin : FIBE tourne en parallèle d'Ecospace tout
            ' le long du couplage. L'exit code est loggé de façon asynchrone.
            AddHandler p.Exited, Sub(sender As Object, e As EventArgs)
                                     Dim proc As Process = DirectCast(sender, Process)
                                     If proc.ExitCode = 0 Then
                                         Me.WriteFibeLog("INFO: FIBE simulation exited normally (code 0)")
                                         m_logger.LogInformation("FIBE simulation exited normally (code 0)")
                                     Else
                                         Me.WriteFibeLog(String.Format("ERROR: FIBE simulation exited with code {0}", proc.ExitCode))
                                         m_logger.LogError("FIBE simulation exited with code {Code}", proc.ExitCode)
                                     End If
                                     RemoveHandler p.OutputDataReceived, AddressOf Me.OnFibeStdOut
                                     RemoveHandler p.ErrorDataReceived, AddressOf Me.OnFibeStdErr
                                 End Sub

            Me.WriteFibeLog("INFO: FIBE simulation launched " & DateTime.Now.ToString("HH:mm:ss"))
            m_logger.LogInformation("FIBE simulation launched")

        End Sub

        Private Sub OnFibeStdOut(sender As Object, e As DataReceivedEventArgs)
            If Not String.IsNullOrEmpty(e.Data) Then Me.WriteFibeLog(e.Data)
        End Sub

        Private Sub OnFibeStdErr(sender As Object, e As DataReceivedEventArgs)
            If Not String.IsNullOrEmpty(e.Data) Then Me.WriteFibeLog(e.Data)
        End Sub

        Private Function FibeLogFilePath() As String
            Return Path.Combine(Path.GetDirectoryName(LoggingContext.LogFile), $"fibe-{DateTime.Now:yyyyMMdd}.log")
        End Function

        Private Sub WriteFibeLog(line As String)
            Try
                Dim logPath As String = Me.FibeLogFilePath()
                Directory.CreateDirectory(Path.GetDirectoryName(logPath))
                SyncLock m_fibeLogSync
                    File.AppendAllText(logPath, String.Format("[{0}] {1}{2}", DateTime.Now.ToString("HH:mm:ss"), line, Environment.NewLine))
                End SyncLock
            Catch ex As Exception
                m_logger.LogError(ex, "Failed to write FIBE log")
            End Try
        End Sub




#End Region ' Control events

#Region " Overrides "

        Public Overrides Sub OnCoreMessage(msg As EwECore.cMessage)
            If ((msg.Source = eCoreComponentType.Core) And (msg.Type = eMessageType.GlobalSettingsChanged)) Then
                Me.UpdateControls()
            End If

            If msg.Source = eCoreComponentType.Ecospace And msg.Type = eMessageType.DataModified Then
                Me.UpdateControls()
            End If

        End Sub

#End Region ' Overrides

#Region " Internals "

        Private Sub UpdateScenarioFormatProviders()

            Dim scenarioDef As cEcospaceScenario = Me.Core.EcospaceScenarios(Me.Core.ActiveEcospaceScenarioIndex)

            ' Connect controls to core data
            Me.m_fpScenarioName = New cPropertyFormatProvider(Me.UIContext, Me.m_tbName, scenarioDef, eVarNameFlags.Name)
            Me.m_fpScenarioDescription = New cPropertyFormatProvider(Me.UIContext, Me.m_tbDescription, scenarioDef, eVarNameFlags.Description)
            Me.m_fpAuthor = New cPropertyFormatProvider(Me.UIContext, Me.m_tbAuthor, scenarioDef, eVarNameFlags.Author)
            Me.m_fpContact = New cPropertyFormatProvider(Me.UIContext, Me.m_tbContact, scenarioDef, eVarNameFlags.Contact)

        End Sub


#End Region ' Internals

    End Class
End Namespace