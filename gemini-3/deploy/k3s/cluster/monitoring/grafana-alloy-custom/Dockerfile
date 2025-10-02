# Stage 1: Build Alloy from source with Go 1.24.2
FROM golang:1.24.2 AS builder

WORKDIR /src
RUN git clone --depth 1 --branch v1.8.3 https://github.com/grafana/alloy.git .
ENV GOPROXY=https://proxy.golang.org,direct
RUN go build -o /alloy .

# Stage 2: Create a minimal runtime image
FROM ubuntu:24.04

RUN apt-get update && apt-get install -y ca-certificates && rm -rf /var/lib/apt/lists/*

COPY --from=builder /alloy /usr/local/bin/alloy

ENTRYPOINT ["/usr/local/bin/alloy"]
CMD ["--help"]