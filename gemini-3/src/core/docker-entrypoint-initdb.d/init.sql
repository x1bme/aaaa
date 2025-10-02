/* --------------------------------------------------
   1) DROP TABLES & VIEWS (Reverse Dependency Order)
   -------------------------------------------------- */
DROP VIEW IF EXISTS Stroke;
DROP VIEW IF EXISTS SystemView;
DROP TABLE IF EXISTS SignalData;
DROP TABLE IF EXISTS Channel;
DROP TABLE IF EXISTS DAU;
DROP TABLE IF EXISTS Valve;

/* --------------------------------------------------
   2) CREATE TABLES
   -------------------------------------------------- */

/* -- Valve: Each Valve has exactly TWO DAUs (Local & Remote) -- */
CREATE TABLE Valve (
    ValveID INT AUTO_INCREMENT PRIMARY KEY,
    Name VARCHAR(255) NOT NULL
);

/* -- DAU: Local and Remote for each Valve -- */
CREATE TABLE DAU (
    DAUID INT AUTO_INCREMENT PRIMARY KEY,
    ValveID INT NOT NULL,
    Type ENUM('Local', 'Remote') NOT NULL,  -- Clearly defines the DAU type
    FOREIGN KEY (ValveID) REFERENCES Valve(ValveID) ON DELETE CASCADE
);

/* -- Channel: Each DAU has its own distinct set of Channels -- */
CREATE TABLE Channel (
    ChannelID INT AUTO_INCREMENT PRIMARY KEY,
    DAUID INT NOT NULL,
    Name VARCHAR(255) NOT NULL,  -- Measurement type (e.g., Thrust, Torque)
    FOREIGN KEY (DAUID) REFERENCES DAU(DAUID) ON DELETE CASCADE
);

/* -- SignalData: Core data table storing raw signals directly -- */
CREATE TABLE SignalData (
    SignalID BIGINT AUTO_INCREMENT PRIMARY KEY,
    ChannelID INT NOT NULL,
    Timestamp BIGINT NOT NULL,
    SampleRate INT UNSIGNED NOT NULL,  -- Changed to unsigned integer
    Data LONGBLOB NOT NULL,  -- Large data stored directly 
    FOREIGN KEY (ChannelID) REFERENCES Channel(ChannelID) ON DELETE CASCADE
);

/* --------------------------------------------------
   3) CREATE VIEWS 
   -------------------------------------------------- */

/* -- Stroke: Represents filtered, human-readable data -- */
CREATE VIEW Stroke AS
SELECT 
    v.ValveID,
    dau_local.DAUID AS LocalDAUID,
    dau_remote.DAUID AS RemoteDAUID,
    sd.Timestamp AS AcquisitionTime,
    GROUP_CONCAT(c.Name) AS ChannelsUsed,
    sd.SampleRate
FROM Valve v
JOIN DAU dau_local ON v.ValveID = dau_local.ValveID AND dau_local.Type = 'Local'
JOIN DAU dau_remote ON v.ValveID = dau_remote.ValveID AND dau_remote.Type = 'Remote'
JOIN Channel c ON c.DAUID IN (dau_local.DAUID, dau_remote.DAUID)
JOIN SignalData sd ON sd.ChannelID = c.ChannelID
GROUP BY v.ValveID, dau_local.DAUID, dau_remote.DAUID, sd.Timestamp, sd.SampleRate;


/* -- SystemView: Shows the Valve, Both DAUs, and Their Channels for a Given DAU -- */
CREATE VIEW SystemView AS
SELECT 
    dau.DAUID AS InputDAUID,
    v.ValveID,
    v.Name AS ValveName,
    dau_local.DAUID AS LocalDAUID,
    dau_local.Type AS LocalDAUType,
    dau_remote.DAUID AS RemoteDAUID,
    dau_remote.Type AS RemoteDAUType,
    GROUP_CONCAT(DISTINCT ch_local.Name) AS LocalChannels,
    GROUP_CONCAT(DISTINCT ch_remote.Name) AS RemoteChannels
FROM DAU dau
JOIN Valve v ON dau.ValveID = v.ValveID
JOIN DAU dau_local ON v.ValveID = dau_local.ValveID AND dau_local.Type = 'Local'
JOIN DAU dau_remote ON v.ValveID = dau_remote.ValveID AND dau_remote.Type = 'Remote'
LEFT JOIN Channel ch_local ON ch_local.DAUID = dau_local.DAUID
LEFT JOIN Channel ch_remote ON ch_remote.DAUID = dau_remote.DAUID
GROUP BY dau.DAUID, v.ValveID, dau_local.DAUID, dau_remote.DAUID;

/* --------------------------------------------------
   4) CREATE INDEXES FOR PERFORMANCE
   -------------------------------------------------- */
CREATE INDEX idx_signaldata_channel_timestamp
  ON SignalData (ChannelID, Timestamp);

CREATE INDEX idx_channel_dau
  ON Channel (DAUID);
