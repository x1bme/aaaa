FROM golang:1.23.8-alpine AS builder
WORKDIR /src
RUN apk add --no-cache git
RUN git clone --branch v1.9.1 https://github.com/prometheus/node_exporter.git .
RUN go build -o /node_exporter .

FROM busybox:1.36-uclibc
COPY --from=builder /node_exporter /bin/node_exporter
EXPOSE 9100
ENTRYPOINT ["/bin/node_exporter"]