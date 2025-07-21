# SimBrief Integration Setup

This document explains how to set up SimBrief integration using the **public SimBrief API** - no authentication required!

## Overview

SimBrief provides a simple public API that allows applications to fetch your latest flight plan data without any complex authentication or API keys. You just need your Navigraph username or SimBrief Pilot ID.

## Quick Setup

### Step 1: Find Your User ID

You need either your **Navigraph username** OR your **SimBrief Pilot ID**:

**Option A: Navigraph Username (Recommended)**
1. Go to [Navigraph Account Settings](https://navigraph.com/account/settings)
2. Look for your "Navigraph Alias" - this is your username
3. Example: `john_pilot` or `aviator123`

**Option B: SimBrief Pilot ID**
1. Go to [SimBrief Account Settings](https://dispatch.simbrief.com/account)
2. Look for your "Pilot ID" - this is a number up to 7 digits
3. Example: `123456` or `7654321`

### Step 2: Configure the Application

1. Run the MSFS OFP Log application
2. Select the SimBrief option from the menu
3. Enter your Navigraph username or SimBrief Pilot ID when prompted
4. The application will save this for future use

### Step 3: Load Flight Plans

1. Create a flight plan on [SimBrief](https://dispatch.simbrief.com/)
2. In the application, select "Get latest flight plan"
3. Your flight plan will be automatically loaded with all waypoints
4. Start monitoring your flight!

## Features

✅ **No Authentication Required** - Uses public SimBrief API  
✅ **Simple Setup** - Just your username or Pilot ID  
✅ **Latest Flight Plan Access** - Automatically gets your most recent plan  
✅ **Waypoint Loading** - All route waypoints loaded for GPS tracking  
✅ **Flight Information Display** - Shows route, airports, cruise altitude  
✅ **Persistent Configuration** - Remembers your settings between sessions  

## How It Works

The application uses SimBrief's public XML/JSON fetcher API:

- **By Username**: `https://www.simbrief.com/api/xml.fetcher.php?username={username}&json=v2`
- **By Pilot ID**: `https://www.simbrief.com/api/xml.fetcher.php?userid={pilot_id}&json=v2`

This API returns your latest OFP (Operational Flight Plan) with all the route information, which the application converts into GPS waypoints for tracking.

## User Experience

### First Time Setup
```
SimBrief User ID not configured.

You need either:
• Your Navigraph username (from https://navigraph.com/account/settings)
• Your SimBrief Pilot ID (from https://dispatch.simbrief.com/account)

Enter your Navigraph username or SimBrief Pilot ID: john_pilot
✅ SimBrief User ID saved: john_pilot
```

### Loading Flight Plans
```
📋 Using stored SimBrief User ID: john_pilot

⏳ Fetching latest flight plan from SimBrief...
Fetching latest flight plan using username: john_pilot
✅ Successfully loaded 12 waypoints from SimBrief!
   Route: KJFK → EGLL
   Ready to monitor your flight from John F Kennedy Intl to London Heathrow
```

## Troubleshooting

### Common Issues

**"Invalid user specified" or HTTP 400 error**
- Check that your username/Pilot ID is correct
- Make sure you have at least one saved flight plan on SimBrief
- Try using your Pilot ID instead of username (or vice versa)

**"No flight plan found"**
- Create a new flight plan on SimBrief first
- Make sure the flight plan was successfully generated
- Try refreshing/re-generating your flight plan on SimBrief

**Can't find your username or Pilot ID?**
- Navigraph username: [Account Settings](https://navigraph.com/account/settings)
- SimBrief Pilot ID: [Account Settings](https://dispatch.simbrief.com/account)

### Clear Configuration

If you need to reset your stored settings:
1. Select option 4 "Clear SimBrief User ID" from the menu
2. Or manually delete: `%APPDATA%\MsfsOfpLog\user_config.json`

## Limitations

The public SimBrief API has some limitations compared to authenticated APIs:

- **Latest Flight Plan Only**: Can only access your most recent flight plan
- **No MSFS .pln Downloads**: Direct download of .pln files is not supported
- **Read-Only Access**: Cannot create or modify flight plans through the API

For .pln file downloads, you'll need to:
1. Visit [SimBrief](https://dispatch.simbrief.com/)
2. Generate your flight plan
3. Click "Download" → "MSFS" to get the .pln file
4. Use that file with other MSFS tools or import manually

## Benefits

This simplified approach offers several advantages:

- **No Setup Complexity**: No OAuth, API keys, or authentication flows
- **Always Works**: No token expiration or refresh issues
- **Distribution Friendly**: No secrets or credentials to manage
- **User Privacy**: Users provide their own credentials directly
- **Fast Access**: Direct API calls without authentication overhead

## Rate Limiting

SimBrief requests that applications:
- Don't poll or repeatedly download flight plans
- Only fetch data in response to user actions
- Be respectful of the free service

The application follows these guidelines by only fetching data when you explicitly choose to load from SimBrief.

## Technical Details

### API Response Format

The application uses the `json=v2` parameter for the most stable JSON response format. The API returns a comprehensive OFP structure including:

- General flight information (flight level, aircraft, etc.)
- Origin and destination airport details
- Complete navlog with all waypoints and coordinates
- Fuel planning, weights, and other operational data

### Data Conversion

The application extracts:
- **Route Information**: Origin/destination airports with names and coordinates
- **Waypoints**: All navlog fixes with identifiers and coordinates  
- **Flight Level**: Converted from FL to feet for MSFS compatibility
- **GPS Tolerance**: Set to 1.0 NM for waypoint tracking

This provides everything needed for accurate flight tracking and GPS fix monitoring within MSFS.
