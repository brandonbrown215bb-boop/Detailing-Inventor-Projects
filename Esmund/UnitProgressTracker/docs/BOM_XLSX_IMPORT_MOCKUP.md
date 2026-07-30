# BOM XLSX import — mockup (Unit Surface Viewer)

Generated from test file: `BOM_FLAT_5E-030269-05_20260319_1057.xlsx`

Design mockup for the upcoming import feature — **not implemented yet**.

## Source workbook

- **Sheet:** Sheet1 (only sheet)
- **Header row:** fixed 11-column layout (same for all BOM_FLAT files)
- **Data rows:** 425

### Column map

| Column | Import |
| --- | --- |
| Part Number | **keep** |
| Quantity | **keep** |
| Unit | **keep** |
| Skid | **keep** |
| Segment | **keep** |
| Description | **keep** |
| Ext. Description | **keep** |
| MAPICS Seqc | drop |
| MAPICS Action | drop |
| MAPICS Response | drop |
| Labor Hours | drop |

## Proposed row filter

| Rule | Effect |
| --- | --- |
| Drop empty part numbers | blank rows |
| Drop `025-`, `026-`, `028-`, `035-`, `007-`, `091-` | hardware/conduit/stock (~147 rows) |
| Keep `5E…` unit root | unit header (1 row) |
| Keep `391-`, `291-`, `486-`, `386-`, `251-` | shop assemblies, coils, panels |
| Drop all `491-` rows | MAPICS factors (~27 rows) |
| Drop `Segment = <--` unless `391-…` | segment inheritance placeholders (109 `<--` rows in source) |

**Result:** 196 kept / 229 dropped

### Kept rows by part prefix

| Prefix | Count |
| --- | --- |
| 391 | 112 |
| 386 | 45 |
| 291 | 35 |
| 486 | 2 |
| 251 | 1 |
| 5E (unit) | 1 |

## Parsed unit header

```json
{
  "Part Number": "5E0302690501000",
  "Quantity": 1,
  "Unit": "5E-030269-05",
  "Skid": "<--",
  "Segment": "<--",
  "Description": "(SALAS) - UML OLNEY 3.3.26 REL",
  "Ext. Description": ""
}
```

## Parsed structure (by Skid)

### Skid: 01 - [FR-MB]

- **Rows kept:** 61
- **Segments:** VESTIBULE - Vestibule / Corridor; FR - Fan (Return); MB - Mixing Box

| Part Number | Quantity | Unit | Skid | Segment | Description | Ext. Description |
| --- | --- | --- | --- | --- | --- | --- |
| 386-30669-000 | 1 | 5E-030269-05 | 01 - [FR-MB] | VESTIBULE - Vestibule / Corridor | 1 HR TIMER, OUTDOOR | CLEAR, POLY, WHILE-IN-USE COVER OUTDOOR |
| 386-30664-000 | 1 | 5E-030269-05 | 01 - [FR-MB] | FR - Fan (Return) | 1HR TIMER/GFI OUTLET,20AMP,IND | INDOOR |
| 386-30664-000 | 1 | 5E-030269-05 | 01 - [FR-MB] | VESTIBULE - Vestibule / Corridor | 1HR TIMER/GFI OUTLET,20AMP,IND | INDOOR |
| 391-20017-001 | 2 | 5E-030269-05 | 01 - [FR-MB] | VESTIBULE - Vestibule / Corridor | 2" OS LATCH ASSY SS | OUTSWING DOOR LATCH SS HARDWARE |
| 391-20017-001 | 2 | 5E-030269-05 | 01 - [FR-MB] | VESTIBULE - Vestibule / Corridor | 2" OS LATCH ASSY SS | OUTSWING DOOR LATCH SS HARDWARE |

### Skid: 02 - [FF1-EE]

- **Rows kept:** 19
- **Segments:** EE - Economizer; FF-1 - Flat Filter; VESTIBULE - Vestibule / Corridor

| Part Number | Quantity | Unit | Skid | Segment | Description | Ext. Description |
| --- | --- | --- | --- | --- | --- | --- |
| 391-20029-001 | 1 | 5E-030269-05 | 02 - [FF1-EE] | EE - Economizer | 3" OS LATCH ASSY SS | OUTSWING DOOR LATCH ASSY SS HARDWARE |
| 391-20031-001 | 1 | 5E-030269-05 | 02 - [FF1-EE] | EE - Economizer | 3" P-L OS LATCH ASSY SS | OUTSWING PAD LOCK DOOR LATCH ASSY SS HARDWARE |
| 391-60228-277 | 1 | 5E-030269-05 | 02 - [FF1-EE] | EE - Economizer | ASY RECONNECT 118X159.5 SST | STD UTL |
| 391-60228-280 | 1 | 5E-030269-05 | 02 - [FF1-EE] | FF-1 - Flat Filter | ASY RECONNECT 118X159.5 SST | STD UTL |
| 391-60228-853 | 1 | 5E-030269-05 | 02 - [FF1-EE] | VESTIBULE - Vestibule / Corridor | ASY RECONNECT 118X93.75 SST | CUWA |

### Skid: 03 - [XA2-FF2-RF-XA1-CC1]

- **Rows kept:** 35
- **Segments:** XA-2 - Access; XA-1 - Access; RF - High Efficiency Filter; CC-1 - Coil (Cooling); VESTIBULE - Vestibule / Corridor; FF-2 - Flat Filter

| Part Number | Quantity | Unit | Skid | Segment | Description | Ext. Description |
| --- | --- | --- | --- | --- | --- | --- |
| 391-20029-001 | 1 | 5E-030269-05 | 03 - [XA2-FF2-RF-XA1-CC1] | XA-2 - Access | 3" OS LATCH ASSY SS | OUTSWING DOOR LATCH ASSY SS HARDWARE |
| 391-20029-001 | 1 | 5E-030269-05 | 03 - [XA2-FF2-RF-XA1-CC1] | XA-1 - Access | 3" OS LATCH ASSY SS | OUTSWING DOOR LATCH ASSY SS HARDWARE |
| 391-20031-001 | 1 | 5E-030269-05 | 03 - [XA2-FF2-RF-XA1-CC1] | XA-2 - Access | 3" P-L OS LATCH ASSY SS | OUTSWING PAD LOCK DOOR LATCH ASSY SS HARDWARE |
| 391-20031-001 | 1 | 5E-030269-05 | 03 - [XA2-FF2-RF-XA1-CC1] | XA-1 - Access | 3" P-L OS LATCH ASSY SS | OUTSWING PAD LOCK DOOR LATCH ASSY SS HARDWARE |
| 391-01056-007 | 1 | 5E-030269-05 | 03 - [XA2-FF2-RF-XA1-CC1] | RF - High Efficiency Filter | ASY F GA-SPC GLV 24X20 YC |  |

### Skid: 04 - [CC2-XA3-HC]

- **Rows kept:** 28
- **Segments:** CC-2 - Coil (Cooling); XA-3 - Access; HC - Coil (Heating); VESTIBULE - Vestibule / Corridor

| Part Number | Quantity | Unit | Skid | Segment | Description | Ext. Description |
| --- | --- | --- | --- | --- | --- | --- |
| 391-20029-001 | 1 | 5E-030269-05 | 04 - [CC2-XA3-HC] | CC-2 - Coil (Cooling) | 3" OS LATCH ASSY SS | OUTSWING DOOR LATCH ASSY SS HARDWARE |
| 391-20029-001 | 1 | 5E-030269-05 | 04 - [CC2-XA3-HC] | XA-3 - Access | 3" OS LATCH ASSY SS | OUTSWING DOOR LATCH ASSY SS HARDWARE |
| 391-20031-001 | 1 | 5E-030269-05 | 04 - [CC2-XA3-HC] | XA-3 - Access | 3" P-L OS LATCH ASSY SS | OUTSWING PAD LOCK DOOR LATCH ASSY SS HARDWARE |
| 391-20031-001 | 1 | 5E-030269-05 | 04 - [CC2-XA3-HC] | CC-2 - Coil (Cooling) | 3" P-L OS LATCH ASSY SS | OUTSWING PAD LOCK DOOR LATCH ASSY SS HARDWARE |
| 391-60228-277 | 1 | 5E-030269-05 | 04 - [CC2-XA3-HC] | HC - Coil (Heating) | ASY RECONNECT 118X159.5 SST | STD UTL |

### Skid: 05 - [DP-FS]

- **Rows kept:** 52
- **Segments:** DP - Discharge Plenum; FS - Fan (Supply); VESTIBULE - Vestibule / Corridor

| Part Number | Quantity | Unit | Skid | Segment | Description | Ext. Description |
| --- | --- | --- | --- | --- | --- | --- |
| 391-20039-001 | 1 | 5E-030269-05 | 05 - [DP-FS] | DP - Discharge Plenum | 3" P-L IS LATCH ASSY SS | INSWING PAD LOCK DOOR LATCH ASSY SS HARDWARE |
| 391-20037-001 | 1 | 5E-030269-05 | 05 - [DP-FS] | DP - Discharge Plenum | 3"IS LATCH ASSY SS | INSWING DOOR LATCH ASSY SS HARDWARE |
| 391-01106-003 | 3 | 5E-030269-05 | 05 - [DP-FS] | FS - Fan (Supply) | AMSR-2D ISO PLT SST 3/4 THR |  |
| 391-01106-003 | 3 | 5E-030269-05 | 05 - [DP-FS] | FS - Fan (Supply) | AMSR-2D ISO PLT SST 3/4 THR |  |
| 391-01106-004 | 3 | 5E-030269-05 | 05 - [DP-FS] | FS - Fan (Supply) | AMSR-2E ISO PLT SST 3/4 THR |  |

## Target in-app object (mock JSON)

```json
{
  "sourceFile": "BOM_FLAT_5E-030269-05_20260319_1057.xlsx",
  "importedAt": "(ISO timestamp on import)",
  "unit": {
    "Part Number": "5E0302690501000",
    "Quantity": 1,
    "Unit": "5E-030269-05",
    "Skid": "<--",
    "Segment": "<--",
    "Description": "(SALAS) - UML OLNEY 3.3.26 REL",
    "Ext. Description": ""
  },
  "filter": {
    "keptColumns": [
      "Part Number",
      "Quantity",
      "Unit",
      "Skid",
      "Segment",
      "Description",
      "Ext. Description"
    ],
    "droppedColumns": [
      "MAPICS Seqc",
      "MAPICS Action",
      "MAPICS Response",
      "Labor Hours"
    ],
    "keptRowCount": 196,
    "droppedRowCount": 229
  },
  "skids": [
    {
      "skidId": "01 - [FR-MB]",
      "segments": [
        "VESTIBULE - Vestibule / Corridor",
        "FR - Fan (Return)",
        "MB - Mixing Box"
      ],
      "partCount": 61,
      "partsSample": [
        {
          "Part Number": "386-30669-000",
          "Quantity": 1,
          "Unit": "5E-030269-05",
          "Skid": "01 - [FR-MB]",
          "Segment": "VESTIBULE - Vestibule / Corridor",
          "Description": "1 HR TIMER, OUTDOOR",
          "Ext. Description": "CLEAR, POLY, WHILE-IN-USE COVER OUTDOOR"
        },
        {
          "Part Number": "386-30664-000",
          "Quantity": 1,
          "Unit": "5E-030269-05",
          "Skid": "01 - [FR-MB]",
          "Segment": "FR - Fan (Return)",
          "Description": "1HR TIMER/GFI OUTLET,20AMP,IND",
          "Ext. Description": "INDOOR"
        },
        {
          "Part Number": "386-30664-000",
          "Quantity": 1,
          "Unit": "5E-030269-05",
          "Skid": "01 - [FR-MB]",
          "Segment": "VESTIBULE - Vestibule / Corridor",
          "Description": "1HR TIMER/GFI OUTLET,20AMP,IND",
          "Ext. Description": "INDOOR"
        }
      ]
    },
    {
      "skidId": "04 - [CC2-XA3-HC]",
      "segments": [
        "CC-2 - Coil (Cooling)",
        "XA-3 - Access",
        "HC - Coil (Heating)",
        "VESTIBULE - Vestibule / Corridor",
        "<--"
      ],
      "partCount": 28,
      "partsSample": [
        {
          "Part Number": "391-20029-001",
          "Quantity": 1,
          "Unit": "5E-030269-05",
          "Skid": "04 - [CC2-XA3-HC]",
          "Segment": "CC-2 - Coil (Cooling)",
          "Description": "3\" OS LATCH ASSY SS",
          "Ext. Description": "OUTSWING DOOR LATCH ASSY SS HARDWARE"
        },
        {
          "Part Number": "391-20029-001",
          "Quantity": 1,
          "Unit": "5E-030269-05",
          "Skid": "04 - [CC2-XA3-HC]",
          "Segment": "XA-3 - Access",
          "Description": "3\" OS LATCH ASSY SS",
          "Ext. Description": "OUTSWING DOOR LATCH ASSY SS HARDWARE"
        },
        {
          "Part Number": "391-20031-001",
          "Quantity": 1,
          "Unit": "5E-030269-05",
          "Skid": "04 - [CC2-XA3-HC]",
          "Segment": "XA-3 - Access",
          "Description": "3\" P-L OS LATCH ASSY SS",
          "Ext. Description": "OUTSWING PAD LOCK DOOR LATCH ASSY SS HARDWARE"
        }
      ]
    },
    {
      "skidId": "03 - [XA2-FF2-RF-XA1-CC1]",
      "segments": [
        "XA-2 - Access",
        "XA-1 - Access",
        "RF - High Efficiency Filter",
        "CC-1 - Coil (Cooling)",
        "VESTIBULE - Vestibule / Corridor",
        "<--",
        "FF-2 - Flat Filter"
      ],
      "partCount": 35,
      "partsSample": [
        {
          "Part Number": "391-20029-001",
          "Quantity": 1,
          "Unit": "5E-030269-05",
          "Skid": "03 - [XA2-FF2-RF-XA1-CC1]",
          "Segment": "XA-2 - Access",
          "Description": "3\" OS LATCH ASSY SS",
          "Ext. Description": "OUTSWING DOOR LATCH ASSY SS HARDWARE"
        },
        {
          "Part Number": "391-20029-001",
          "Quantity": 1,
          "Unit": "5E-030269-05",
          "Skid": "03 - [XA2-FF2-RF-XA1-CC1]",
          "Segment": "XA-1 - Access",
          "Description": "3\" OS LATCH ASSY SS",
          "Ext. Description": "OUTSWING DOOR LATCH ASSY SS HARDWARE"
        },
        {
          "Part Number": "391-20031-001",
          "Quantity": 1,
          "Unit": "5E-030269-05",
          "Skid": "03 - [XA2-FF2-RF-XA1-CC1]",
          "Segment": "XA-2 - Access",
          "Description": "3\" P-L OS LATCH ASSY SS",
          "Ext. Description": "OUTSWING PAD LOCK DOOR LATCH ASSY SS HARDWARE"
        }
      ]
    },
    {
      "skidId": "02 - [FF1-EE]",
      "segments": [
        "EE - Economizer",
        "FF-1 - Flat Filter",
        "VESTIBULE - Vestibule / Corridor"
      ],
      "partCount": 19,
      "partsSample": [
        {
          "Part Number": "391-20029-001",
          "Quantity": 1,
          "Unit": "5E-030269-05",
          "Skid": "02 - [FF1-EE]",
          "Segment": "EE - Economizer",
          "Description": "3\" OS LATCH ASSY SS",
          "Ext. Description": "OUTSWING DOOR LATCH ASSY SS HARDWARE"
        },
        {
          "Part Number": "391-20031-001",
          "Quantity": 1,
          "Unit": "5E-030269-05",
          "Skid": "02 - [FF1-EE]",
          "Segment": "EE - Economizer",
          "Description": "3\" P-L OS LATCH ASSY SS",
          "Ext. Description": "OUTSWING PAD LOCK DOOR LATCH ASSY SS HARDWARE"
        },
        {
          "Part Number": "391-60228-277",
          "Quantity": 1,
          "Unit": "5E-030269-05",
          "Skid": "02 - [FF1-EE]",
          "Segment": "EE - Economizer",
          "Description": "ASY RECONNECT 118X159.5 SST",
          "Ext. Description": "STD UTL"
        }
      ]
    },
    {
      "skidId": "05 - [DP-FS]",
      "segments": [
        "DP - Discharge Plenum",
        "FS - Fan (Supply)",
        "VESTIBULE - Vestibule / Corridor"
      ],
      "partCount": 52,
      "partsSample": [
        {
          "Part Number": "391-20039-001",
          "Quantity": 1,
          "Unit": "5E-030269-05",
          "Skid": "05 - [DP-FS]",
          "Segment": "DP - Discharge Plenum",
          "Description": "3\" P-L IS LATCH ASSY SS",
          "Ext. Description": "INSWING PAD LOCK DOOR LATCH ASSY SS HARDWARE"
        },
        {
          "Part Number": "391-20037-001",
          "Quantity": 1,
          "Unit": "5E-030269-05",
          "Skid": "05 - [DP-FS]",
          "Segment": "DP - Discharge Plenum",
          "Description": "3\"IS LATCH ASSY SS",
          "Ext. Description": "INSWING DOOR LATCH ASSY SS HARDWARE"
        },
        {
          "Part Number": "391-01106-003",
          "Quantity": 3,
          "Unit": "5E-030269-05",
          "Skid": "05 - [DP-FS]",
          "Segment": "FS - Fan (Supply)",
          "Description": "AMSR-2D ISO PLT SST 3/4 THR",
          "Ext. Description": ""
        }
      ]
    }
  ]
}
```

## Planned UI flow

1. **File → Import BOM…** — pick `.xlsx`
2. Parse Sheet1 with fixed column names
3. Apply filters above; show summary counts
4. Store/display by Skid → Segment → parts
5. Later: cross-link to loaded 391Z surfaces

## Open questions

- Match `291-` coil rows to surfaces or keep unit-level only?
- Collapse duplicate part+skid+segment lines or keep separate qty rows?
- Map `391-602xx` panel parts to 391Z surface IAM numbers — rules TBD
