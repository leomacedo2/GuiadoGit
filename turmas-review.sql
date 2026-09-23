START TRANSACTION;

ALTER TABLE portfolio."RepositoryAnalysis" ADD "PushedAt" timestamp with time zone;

CREATE TABLE portfolio."Classrooms" (
    "Id" uuid NOT NULL,
    "ApplicationUserId" text NOT NULL,
    "Name" character varying(100) NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_Classrooms" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_Classrooms_Id_ApplicationUserId" UNIQUE ("Id", "ApplicationUserId"),
    CONSTRAINT "CK_Classrooms_Name" CHECK (char_length(btrim("Name")) BETWEEN 2 AND 100),
    CONSTRAINT "FK_Classrooms_AspNetUsers_ApplicationUserId" FOREIGN KEY ("ApplicationUserId") REFERENCES portfolio."AspNetUsers" ("Id") ON DELETE CASCADE
);

CREATE TABLE portfolio."ClassroomMembers" (
    "ClassroomId" uuid NOT NULL,
    "GitHubProfileId" uuid NOT NULL,
    "ApplicationUserId" text NOT NULL,
    "AddedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_ClassroomMembers" PRIMARY KEY ("ClassroomId", "GitHubProfileId"),
    CONSTRAINT "FK_ClassroomMembers_Classrooms_ClassroomId_ApplicationUserId" FOREIGN KEY ("ClassroomId", "ApplicationUserId") REFERENCES portfolio."Classrooms" ("Id", "ApplicationUserId") ON DELETE CASCADE,
    CONSTRAINT "FK_ClassroomMembers_UserSavedProfiles" FOREIGN KEY ("ApplicationUserId", "GitHubProfileId") REFERENCES portfolio."UserSavedProfiles" ("UserId", "GitHubProfileId")
);

CREATE INDEX "IX_ClassroomMembers_ApplicationUserId_GitHubProfileId" ON portfolio."ClassroomMembers" ("ApplicationUserId", "GitHubProfileId");

CREATE INDEX "IX_ClassroomMembers_ClassroomId_ApplicationUserId" ON portfolio."ClassroomMembers" ("ClassroomId", "ApplicationUserId");

CREATE INDEX "IX_Classrooms_ApplicationUserId_UpdatedAt" ON portfolio."Classrooms" ("ApplicationUserId", "UpdatedAt");

INSERT INTO portfolio."__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260923151210_AddClassroomsAndRepositoryPushActivity', '8.0.31');

COMMIT;

