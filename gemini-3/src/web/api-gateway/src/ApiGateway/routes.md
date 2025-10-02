### Page: Login
API calls needed:
-   *GET auth, with encrypted user login information for logging in 
-   *GET user/id, returns the user configs and applies them to the page after auth
### Page: Valves
API calls needed:
-   *GET vavles, returns all valves
### Page: Valve Status
API calls needed:
-   *GET valves, returns all valves
### Page: Valve Logs
API calls needed:
-   *GET valve/logs, returns all valve logs from loom sys logs
### Page: Valve Configs
API calls needed:
-   *GET valves/config, returns all valves with their configurations
-   *POST valves, Add a new valve to the system
-   *GET valves/id, get a valve's configurations, can also be used to **Export** valve settings to csv, this could also be client side
-   *PUT valves/id, update a valve's configurations
-   *DELETE valves/id, delete a valve
## Page: Web Interface Configuration
API calls needed:
-   GET WIConfig, gets the current web interface configuration
-   PUT WIConfig, updates the web interface configuration
### Page: Database Interface
API calls needed:
-   *GET databases, gets active dbs from db interface
### Page: Gemini Network
API calls needed:
-   *GET vavles, returns all valves
## Page: Security
Need more info
### Page: User Settings
API calls needed:
-   *POST user, creates new user
-   *PUT user/id, Updates the user config
-   *DELETE user/id, Deletes user by id

