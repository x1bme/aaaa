FROM alpine:3.21.3

# Install necessary packages (add any others needed by Tempo)
RUN apk add --no-cache ca-certificates tzdata

ADD https://github.com/grafana/tempo/releases/download/v2.4.1/tempo_2.4.1_linux_amd64.tar.gz /tmp/
RUN tar -xzf /tmp/tempo_2.4.1_linux_amd64.tar.gz -C /usr/local/bin && \
    rm /tmp/tempo_2.4.1_linux_amd64.tar.gz

ENTRYPOINT ["/usr/local/bin/tempo"]
CMD ["--help"]