FROM alpine:3.22

RUN apk add --no-cache openssh-server openssh-keygen \
    && adduser -D -s /bin/sh tester \
    && echo 'tester:tester-password' | chpasswd

COPY sshd_config /etc/ssh/sshd_config
COPY entrypoint.sh /usr/local/bin/entrypoint.sh

RUN sed -i 's/\r$//' /etc/ssh/sshd_config /usr/local/bin/entrypoint.sh \
    && chmod 755 /usr/local/bin/entrypoint.sh

EXPOSE 22

ENTRYPOINT ["/usr/local/bin/entrypoint.sh"]
