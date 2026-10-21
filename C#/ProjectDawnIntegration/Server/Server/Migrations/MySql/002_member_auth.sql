CREATE TABLE IF NOT EXISTS accountcredential (
 AccountDbId int NOT NULL PRIMARY KEY,
 LoginId varchar(32) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 PasswordSalt varbinary(32) NOT NULL,
 PasswordHash varbinary(32) NOT NULL,
 Iterations int NOT NULL,
 HashAlgorithm varchar(32) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 CreatedUtc datetime(6) NOT NULL,
 LastLoginUtc datetime(6) NULL,
 Disabled tinyint(1) NOT NULL DEFAULT 0,
 UNIQUE KEY UX_AccountCredential_LoginId(LoginId),
 CONSTRAINT FK_AccountCredential_Account FOREIGN KEY(AccountDbId) REFERENCES account(AccountDbId),
 CONSTRAINT CK_AccountCredential_Iterations CHECK(Iterations BETWEEN 600000 AND 2000000)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
CREATE TABLE IF NOT EXISTS authregistrationgate (
 Id int NOT NULL PRIMARY KEY,
 CONSTRAINT CK_AuthRegistrationGate_Id CHECK(Id=1)
) ENGINE=InnoDB;
INSERT IGNORE INTO authregistrationgate(Id) VALUES(1);
