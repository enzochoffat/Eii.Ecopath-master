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

            ' FIBE coupling: build the fleet selection list
            Me.InitializeFIBEFleetList()

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

            ' FIBE coupling: show/hide the fleet selection
            If Me.m_clbFIBEFleets IsNot Nothing Then
                Me.m_clbFIBEFleets.Visible = Me.m_Couplage.Checked
            End If

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
            End If
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
                Me.RunInstallScript(fileName)
            End If

            Dim count As Integer = timeStep * 28 - 28

            Dim basePath As String = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..", "..", "Couplage", "FIBE", "diatome", "results", "biomass")
            Dim targetFileName As String = $"agent_{count}.csv"
            Dim fullPath As String = System.IO.Path.Combine(basePath, targetFileName)
            Dim runTime As TextBox = Me.m_tbTotalTime
            Dim valueRunTime As Integer = CInt(runTime.Text.Replace("""", ""))
            Dim iFirstYear As Integer = Me.Core.EcosimFirstYear()

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
            Me.RunPostSaveScript(fileName, ts.iTimeStep, valueRunTime, iFirstYear)

            If timeStep > 1 Then
                Debug.WriteLine($"Waiting for file: {targetFileName}")
                While Not System.IO.File.Exists(fullPath)
                    System.Threading.Thread.Sleep(2000) ' Pause de 2 secondes
                End While
                Debug.WriteLine($"File found: {targetFileName}")

                ' FIBE coupling: read the fishing mortality exported by FIBE for this month
                ' (F_<count>.csv is written by FIBE at the same time as the agent file)
                Me.LoadFIBEFishingMortality(count, basePath)
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

        Private Function GetInstallScriptContent() As String
            Dim sb As New System.Text.StringBuilder()
            sb.AppendLine("$scriptDir = $PSScriptRoot")
            sb.AppendLine("$scriptDirParent = (Get-Item $scriptDir).Parent.Parent.FullName")
            sb.AppendLine("$fibePath = Join-Path $scriptDirParent ""FIBE\diatome""")
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
            Dim scriptInstallPath As String = Path.Combine(scriptDir, "install.ps1")

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

        Private Function GetPostSaveScriptContent() As String
            Dim sb As New System.Text.StringBuilder()
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
            sb.AppendLine("    [string]$LogFile")
            sb.AppendLine(")")
            sb.AppendLine("")
            sb.AppendLine("New-Item -Path (Split-Path $LogFile) -ItemType Directory -Force | Out-Null")
            sb.AppendLine("")
            sb.AppendLine("function Run-Python([string]$Script, [string]$Arg) {")
            sb.AppendLine("    try {")
            sb.AppendLine("        $out = & python $Script $Arg 2>&1 | ForEach-Object { $_.ToString() } | Out-String")
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

            sb.AppendLine("try {")
            sb.AppendLine("    $out = & "".\..\..\CreateJSON.ps1"" $TimeStep $runTime $FirstYear 2>&1 | Out-String")
            sb.AppendLine("} catch {")
            sb.AppendLine("    $out = ""EXCEPTION: "" + $_.Exception.Message")
            sb.AppendLine("}")
            sb.AppendLine("$out | Out-File -FilePath $LogFile -Append -Encoding utf8")

            Return sb.ToString()
        End Function

        Private Sub RunPostSaveScript(fileName As String, timeStep As Integer, valueRunTime As Integer, iFirstYear As Integer)

            Dim scriptDir As String = Path.GetDirectoryName(fileName)
            Dim scriptPath As String = Path.Combine(scriptDir, "post_save.ps1")
            Dim logFilePath As String = Path.Combine(Path.GetDirectoryName(LoggingContext.LogFile), $"couplage-{DateTime.Now:yyyyMMdd}.log")

            ' Toujours régénérer le script : il doit rester synchronisé avec le
            ' code (ajout/retrait d'étapes de conversion).
            Dim content As String = Me.GetPostSaveScriptContent()
            File.WriteAllText(scriptPath, content)
            m_logger.LogInformation("Post save script created at {Path}", scriptPath)

            Dim psi As New ProcessStartInfo()
            psi.FileName = "powershell.exe"
            psi.Arguments = String.Format("-ExecutionPolicy Bypass -File ""{0}"" ""{1}"" {2} {3} {4} ""{5}""", scriptPath, fileName, timeStep, valueRunTime, iFirstYear, logFilePath)
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
            Dim diatomePath As String = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "..", "..", "Couplage", "FIBE", "diatome"))
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