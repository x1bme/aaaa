FROM golang:1.23.8-alpine AS builder
WORKDIR /src
RUN apk add --no-cache git
RUN git clone --branch v2.15.0 https://github.com/kubernetes/kube-state-metrics.git .
# Update vulnerable Go modules
RUN go get golang.org/x/oauth2@v0.27.0
RUN go get golang.org/x/crypto@v0.35.0
RUN go mod tidy
RUN go build -o /kube-state-metrics .

FROM busybox:1.36-uclibc
COPY --from=builder /kube-state-metrics /bin/kube-state-metrics
EXPOSE 8080
ENTRYPOINT ["/bin/kube-state-metrics"]