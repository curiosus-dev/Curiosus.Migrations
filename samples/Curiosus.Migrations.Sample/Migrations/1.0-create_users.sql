CREATE TABLE users
(
    id      BIGSERIAL PRIMARY KEY,
    name    TEXT        NOT NULL,
    created TIMESTAMPTZ NOT NULL DEFAULT now()
);
