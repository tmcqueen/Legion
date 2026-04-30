FROM postgres:latest

RUN apt update

RUN apt install -y postgresql-18-pgvector

COPY init.sql /docker-entrypoint-initdb.d/