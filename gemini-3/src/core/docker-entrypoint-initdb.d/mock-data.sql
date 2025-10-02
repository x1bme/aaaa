/* (A) Insert one Valve */
INSERT INTO Valve (Name)
VALUES ('Valve A');

/* (B) Insert two DAUs for Valve A: local and remote */
INSERT INTO DAU (ValveID, Type) VALUES (1, 'Local');   -- DAUID = 1
INSERT INTO DAU (ValveID, Type) VALUES (1, 'Remote');  -- DAUID = 2

/* (C) Insert Channels for each DAU */
-- Local DAU (DAUID = 1) gets:
INSERT INTO Channel (DAUID, Name) VALUES (1, 'Thrust');  -- ChannelID = 1 (Local Thrust)
INSERT INTO Channel (DAUID, Name) VALUES (1, 'Torque');  -- ChannelID = 2 (Local Torque)

-- Remote DAU (DAUID = 2) gets:
INSERT INTO Channel (DAUID, Name) VALUES (2, 'Current');  -- ChannelID = 3 (Remote Current)

/* (D) Insert Signals for each channel */
-- INSERT INTO SignalData (ChannelID, Timestamp, SampleRate, Data)
-- VALUES (1, 100000, 1000, 'Local Thrust Data');  -- SignalID = 1 (Local Thrust)
-- INSERT INTO SignalData (ChannelID, Timestamp, SampleRate, Data)
-- VALUES (2, 100010, 1000, 'Local Torque Data');  -- SignalID = 2 (Local Torque)
-- INSERT INTO SignalData (ChannelID, Timestamp, SampleRate, Data)
-- VALUES (3, 100020, 1000, 'Remote Current Data'); -- SignalID = 3 (Remote Current)