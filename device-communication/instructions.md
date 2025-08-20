# Instructions

#### (-2) Install Docker
See [Docker installation](https://docs.docker.com/engine/install/ubuntu/)

#### (-1) Install grpcurl
See [grpc installation](https://github.com/fullstorydev/grpcurl)

#### (0) Downloads tarball and shell script
Download both the docker image tarball (crane-server-sim.tar) and 
shell script (crane_grpc_cli.sh) to your machine
Commands below assume both the tarball and shell script are in the current
working directory.

#### (1) Load the tarball
```
docker load -i crane-server-sim.tar
```

#### (2) Run it
NOTE: <host-port>:<container-port>
* 5002: host port for your grpc client (grpcurl) on your machine
* 8080: container port for the grpc service
* 12345: host port that your nucleo/DAU device connects to
* 12345: also container port that the server listens on via TCP

If your nucleo device is on the network and not on the docker host, 
then connect it to docker_host_machine_ip:12345. Otherwise, localhost:12345 
would suffice. Name your container anything you'd like, it's cranetainer (heh) here.
```
docker run -d --name cranetainer \
  -p 5002:8080 \ 
  -p 12345:12345 \
  crane-server-sim:latest
```

#### (3) Prepare the grpc cli

#### (3.1) Source the shell script
Source the crane_grpc_cli.sh file so you have 
access to some convenient shell functions. This should
ease testing, as I'm directly naming the functions
based on the calls in R0.8 Firmware release plan 3452F02, Rev. 4
(names are slightly different, e.g., I'm using DataGet rather 
than GetData, but mostly the same)
```
source crane_grpc_cli.sh
```

#### (3.2) Help to get existing functions
NOTE: All functions require an input argument,
such as the device_id, request contents, etc.
To make things easier to edit, most of these
arguments (in JSON) are in the (generated) grpc_payloads
directory. Most of these commands use these as
payloads, but feel free to modify for testing. Defaults
have been provided already.
```
grpcHelp
```

#### (3.3) Run R0.8 Heartbeat command
```
grpcHeartbeat
```

#### (3.4) Run R0.8 grpcHealthStatus command
```
grpcHealthStatus
```

#### (3.5) Run commands and view server's stdout
For some commands, such as grpcDataGet,
the server currently just outputs what it gets
to stdout, while the grpcservice only tells your grpc client
whether the call to DataGet was successful or not.
Since the server's stdout is inside the container, run
docker logs to see what it's outputting. 
Note that you may see the stdout that I was using to 
test my own client, so not all of the output is due to the 
response from your device!
```
grpcDataGet
docker logs -f cranetainer
```

#### (3.6) Restart cranetainer
If the cranetainer crashes for any reason, then restart it
using
```
docker kill cranetainer
docker container rm cranetainer
docker run -d --name cranetainer -p 12345:12345 -p 5002:8080 crane-server-sim:latest
```
Feel free to put these three commands inside of a script that you can conveniently run
when needed. It is possible to restart it using a single `docker restart cranetainer`
as well, but this may not always work.
