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
Imports Newtonsoft.Json

#End Region ' Imports

''' ---------------------------------------------------------------------------
''' <summary>
''' A single restricted area: a named geographic zone, defined by a shapefile.
''' </summary>
''' ---------------------------------------------------------------------------
Public Class cRestrictedAreaZone

    ''' <summary>
    ''' Name of the zone. This name is used as the key in the diatome
    ''' "restricted_area_map" configuration entry.
    ''' </summary>
    Public Property Name As String = ""

    ''' <summary>
    ''' Full path to the zone shapefile (.shp).
    ''' </summary>
    Public Property ShapefilePath As String = ""

    ''' -----------------------------------------------------------------------
    ''' <summary>
    ''' Provide a friendly description for lists and grids.
    ''' </summary>
    ''' -----------------------------------------------------------------------
    Public Overrides Function ToString() As String
        If (String.IsNullOrWhiteSpace(Me.ShapefilePath)) Then Return Me.Name
        Return String.Format("{0}  [{1}]", Me.Name, Me.ShapefilePath)
    End Function

End Class

''' ---------------------------------------------------------------------------
''' <summary>
''' Container for the restricted areas configuration of the FIBE coupling.
''' </summary>
''' <remarks>
''' <para>Holds the list of geographic zones (shapefiles) and the per-fleet,
''' per-month restriction matrix. Both parts are serialized to JSON strings
''' and stored in <see cref="cEcospaceModelParameters"/> so they survive
''' saving and loading of the model.</para>
''' <para>The restriction matrix maps a fleet name (as used by diatome, e.g.
''' "archipelago", "coastal", "trawler") to a zone x month matrix. Month
''' columns run January (0) to December (11). Values follow the diatome
''' encoding: 0 = closed ("fermé"), 1 = navigation only ("navigable"),
''' 2 = open ("ouvert").</para>
''' </remarks>
''' ---------------------------------------------------------------------------
Public Class cRestrictedAreasConfig

    ''' <summary>
    ''' Number of months in the restriction matrix.
    ''' </summary>
    Public Const nMonths As Integer = 12

    ''' <summary>
    ''' Fleet types used by diatome. Each fleet has its own restriction matrix.
    ''' </summary>
    Public Shared ReadOnly FIBEFleetTypes() As String = {"archipelago", "coastal", "trawler"}

    ''' <summary>
    ''' The geographic zones.
    ''' </summary>
    Public Property Zones As List(Of cRestrictedAreaZone) = New List(Of cRestrictedAreaZone)

    ''' <summary>
    ''' Per-fleet restriction matrix. Each value is a zone x month matrix
    ''' (jagged arrays serialize cleanly to JSON).
    ''' </summary>
    Public Property Vector As Dictionary(Of String, Integer()()) = New Dictionary(Of String, Integer()())

#Region " Map (zones) serialization "

    ''' -----------------------------------------------------------------------
    ''' <summary>
    ''' Serialize the zones to a JSON string, in the format expected by the
    ''' diatome "maps.restricted_area_map" entry:
    ''' <c>{ "zone_1": "path1", "zone_2": "path2" }</c>.
    ''' </summary>
    ''' -----------------------------------------------------------------------
    Public Function SerializeMap() As String
        Dim dict As New Dictionary(Of String, String)
        For Each zone As cRestrictedAreaZone In Me.Zones
            If (Not String.IsNullOrWhiteSpace(zone.Name)) Then
                dict(zone.Name) = zone.ShapefilePath
            End If
        Next
        Return JsonConvert.SerializeObject(dict, Formatting.None)
    End Function

    ''' -----------------------------------------------------------------------
    ''' <summary>
    ''' Restore the zones from a JSON string.
    ''' </summary>
    ''' <param name="strJSON">JSON in the diatome "restricted_area_map" format,
    ''' or Nothing/empty to restore an empty list.</param>
    ''' -----------------------------------------------------------------------
    Public Sub DeserializeMap(strJSON As String)
        Me.Zones = New List(Of cRestrictedAreaZone)
        If (String.IsNullOrWhiteSpace(strJSON)) Then Return
        Try
            Dim dict As Dictionary(Of String, String) = JsonConvert.DeserializeObject(Of Dictionary(Of String, String))(strJSON)
            If (dict Is Nothing) Then Return
            For Each kv As KeyValuePair(Of String, String) In dict
                Me.Zones.Add(New cRestrictedAreaZone With {.Name = kv.Key, .ShapefilePath = kv.Value})
            Next
        Catch ex As Exception
            Me.Zones = New List(Of cRestrictedAreaZone)
        End Try
    End Sub

#End Region ' Map (zones) serialization

#Region " Vector (matrix) serialization "

    ''' -----------------------------------------------------------------------
    ''' <summary>
    ''' Serialize the per-fleet restriction matrices to a JSON string.
    ''' </summary>
    ''' -----------------------------------------------------------------------
    Public Function SerializeVector() As String
        Return JsonConvert.SerializeObject(Me.Vector, Formatting.None)
    End Function

    ''' -----------------------------------------------------------------------
    ''' <summary>
    ''' Restore the per-fleet restriction matrices from a JSON string.
    ''' </summary>
    ''' <param name="strJSON">JSON in the format produced by
    ''' <see cref="SerializeVector"/>.</param>
    ''' -----------------------------------------------------------------------
    Public Sub DeserializeVector(strJSON As String)
        Me.Vector = New Dictionary(Of String, Integer()())
        If (String.IsNullOrWhiteSpace(strJSON)) Then Return
        Try
            Dim dict As Dictionary(Of String, Integer()()) = JsonConvert.DeserializeObject(Of Dictionary(Of String, Integer()()))(strJSON)
            If (dict Is Nothing) Then Return
            Me.Vector = dict
        Catch ex As Exception
            Me.Vector = New Dictionary(Of String, Integer()())
        End Try
    End Sub

    ''' -----------------------------------------------------------------------
    ''' <summary>
    ''' Returns a zone x month matrix for a fleet, creating it when missing.
    ''' </summary>
    ''' <param name="fleetName">Fleet name as used by diatome.</param>
    ''' <param name="nZones">Number of zones (rows) the new matrix must have.</param>
    ''' <returns>The existing matrix, or a new one filled with
    ''' <see cref="eRestrictedAreaStatus.Open"/>.</returns>
    ''' -----------------------------------------------------------------------
    Public Function GetOrCreateVector(fleetName As String, nZones As Integer) As Integer()()
        If (Me.Vector.ContainsKey(fleetName)) Then Return Me.Vector(fleetName)
        Dim matrix(nZones - 1)() As Integer
        For i As Integer = 0 To nZones - 1
            matrix(i) = New Integer(nMonths - 1) {}
            For m As Integer = 0 To nMonths - 1
                matrix(i)(m) = CInt(eRestrictedAreaStatus.Open)
            Next
        Next
        Me.Vector(fleetName) = matrix
        Return matrix
    End Function

#End Region ' Vector (matrix) serialization

End Class

''' ---------------------------------------------------------------------------
''' <summary>
''' Restriction status of a zone for a fleet in a given month.
''' </summary>
''' <remarks>Values match the encoding used by diatome
''' (see STATUS_ENCODING in restricted_areas.py).</remarks>
''' ---------------------------------------------------------------------------
Public Enum eRestrictedAreaStatus As Integer
    ''' <summary>Fishing prohibited ("fermé").</summary>
    Closed = 0
    ''' <summary>Navigation only, no fishing ("navigable").</summary>
    Navigation = 1
    ''' <summary>Open to fishing ("ouvert").</summary>
    Open = 2
End Enum
