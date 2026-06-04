{
  "openapi": "3.0.1",
  "info": {
    "title": "RecruitPro.API",
    "version": "1.0"
  },
  "paths": {
    "/api/auth/login": {
      "post": {
        "tags": [
          "Auth"
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/LoginRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/LoginRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/LoginRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/api/auth/candidate/login": {
      "post": {
        "tags": [
          "Auth"
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/CandidateLoginRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/CandidateLoginRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/CandidateLoginRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/api/auth/internal/login": {
      "post": {
        "tags": [
          "Auth"
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/InternalLoginRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/InternalLoginRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/InternalLoginRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/api/candidates/register": {
      "post": {
        "tags": [
          "Candidate"
        ],
        "requestBody": {
          "content": {
            "multipart/form-data": {
              "schema": {
                "required": [
                  "UserInfo.Email",
                  "UserInfo.FullName",
                  "UserInfo.PasswordHash"
                ],
                "type": "object",
                "properties": {
                  "UserInfo.FullName": {
                    "maxLength": 100,
                    "minLength": 0,
                    "type": "string"
                  },
                  "UserInfo.Email": {
                    "type": "string",
                    "format": "email"
                  },
                  "UserInfo.Phone": {
                    "pattern": "^0\\d{9}$",
                    "type": "string"
                  },
                  "UserInfo.PasswordHash": {
                    "maxLength": 100,
                    "minLength": 6,
                    "type": "string"
                  },
                  "Profile.CurrentPosition": {
                    "maxLength": 100,
                    "minLength": 0,
                    "type": "string"
                  },
                  "Profile.ExperienceYears": {
                    "maximum": 50,
                    "minimum": 0,
                    "type": "integer",
                    "format": "int32"
                  },
                  "Profile.Education": {
                    "maxLength": 200,
                    "minLength": 0,
                    "type": "string"
                  },
                  "Profile.Address": {
                    "maxLength": 250,
                    "minLength": 0,
                    "type": "string"
                  },
                  "Profile.Bio": {
                    "maxLength": 1000,
                    "minLength": 0,
                    "type": "string"
                  },
                  "Profile.GitHubUrl": {
                    "type": "string",
                    "format": "uri"
                  },
                  "Profile.LinkedInUrl": {
                    "type": "string",
                    "format": "uri"
                  },
                  "resume": {
                    "type": "string",
                    "format": "binary"
                  }
                }
              },
              "encoding": {
                "UserInfo.FullName": {
                  "style": "form"
                },
                "UserInfo.Email": {
                  "style": "form"
                },
                "UserInfo.Phone": {
                  "style": "form"
                },
                "UserInfo.PasswordHash": {
                  "style": "form"
                },
                "Profile.CurrentPosition": {
                  "style": "form"
                },
                "Profile.ExperienceYears": {
                  "style": "form"
                },
                "Profile.Education": {
                  "style": "form"
                },
                "Profile.Address": {
                  "style": "form"
                },
                "Profile.Bio": {
                  "style": "form"
                },
                "Profile.GitHubUrl": {
                  "style": "form"
                },
                "Profile.LinkedInUrl": {
                  "style": "form"
                },
                "resume": {
                  "style": "form"
                }
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/api/candidate/register": {
      "post": {
        "tags": [
          "Candidate"
        ],
        "requestBody": {
          "content": {
            "multipart/form-data": {
              "schema": {
                "required": [
                  "UserInfo.Email",
                  "UserInfo.FullName",
                  "UserInfo.PasswordHash"
                ],
                "type": "object",
                "properties": {
                  "UserInfo.FullName": {
                    "maxLength": 100,
                    "minLength": 0,
                    "type": "string"
                  },
                  "UserInfo.Email": {
                    "type": "string",
                    "format": "email"
                  },
                  "UserInfo.Phone": {
                    "pattern": "^0\\d{9}$",
                    "type": "string"
                  },
                  "UserInfo.PasswordHash": {
                    "maxLength": 100,
                    "minLength": 6,
                    "type": "string"
                  },
                  "Profile.CurrentPosition": {
                    "maxLength": 100,
                    "minLength": 0,
                    "type": "string"
                  },
                  "Profile.ExperienceYears": {
                    "maximum": 50,
                    "minimum": 0,
                    "type": "integer",
                    "format": "int32"
                  },
                  "Profile.Education": {
                    "maxLength": 200,
                    "minLength": 0,
                    "type": "string"
                  },
                  "Profile.Address": {
                    "maxLength": 250,
                    "minLength": 0,
                    "type": "string"
                  },
                  "Profile.Bio": {
                    "maxLength": 1000,
                    "minLength": 0,
                    "type": "string"
                  },
                  "Profile.GitHubUrl": {
                    "type": "string",
                    "format": "uri"
                  },
                  "Profile.LinkedInUrl": {
                    "type": "string",
                    "format": "uri"
                  },
                  "resume": {
                    "type": "string",
                    "format": "binary"
                  }
                }
              },
              "encoding": {
                "UserInfo.FullName": {
                  "style": "form"
                },
                "UserInfo.Email": {
                  "style": "form"
                },
                "UserInfo.Phone": {
                  "style": "form"
                },
                "UserInfo.PasswordHash": {
                  "style": "form"
                },
                "Profile.CurrentPosition": {
                  "style": "form"
                },
                "Profile.ExperienceYears": {
                  "style": "form"
                },
                "Profile.Education": {
                  "style": "form"
                },
                "Profile.Address": {
                  "style": "form"
                },
                "Profile.Bio": {
                  "style": "form"
                },
                "Profile.GitHubUrl": {
                  "style": "form"
                },
                "Profile.LinkedInUrl": {
                  "style": "form"
                },
                "resume": {
                  "style": "form"
                }
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/api/candidate/dashboard": {
      "get": {
        "tags": [
          "Candidate"
        ],
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/api/candidate/applications": {
      "get": {
        "tags": [
          "Candidate"
        ],
        "parameters": [
          {
            "name": "page",
            "in": "query",
            "schema": {
              "type": "integer",
              "format": "int32",
              "default": 1
            }
          },
          {
            "name": "pageSize",
            "in": "query",
            "schema": {
              "type": "integer",
              "format": "int32",
              "default": 10
            }
          },
          {
            "name": "status",
            "in": "query",
            "schema": {
              "type": "string"
            }
          },
          {
            "name": "keyword",
            "in": "query",
            "schema": {
              "type": "string"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/api/candidate/applications/{applicationId}/withdraw": {
      "post": {
        "tags": [
          "Candidate"
        ],
        "parameters": [
          {
            "name": "applicationId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/api/candidate/profile": {
      "get": {
        "tags": [
          "Candidate"
        ],
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      },
      "put": {
        "tags": [
          "Candidate"
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/UpdateCandidateProfileRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/UpdateCandidateProfileRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/UpdateCandidateProfileRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/api/candidate/profile/skills": {
      "put": {
        "tags": [
          "Candidate"
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/UpdateCandidateSkillsRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/UpdateCandidateSkillsRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/UpdateCandidateSkillsRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/api/candidate/profile/resume": {
      "post": {
        "tags": [
          "Candidate"
        ],
        "requestBody": {
          "content": {
            "multipart/form-data": {
              "schema": {
                "type": "object",
                "properties": {
                  "resume": {
                    "type": "string",
                    "format": "binary"
                  }
                }
              },
              "encoding": {
                "resume": {
                  "style": "form"
                }
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/api/hr/dashboard": {
      "get": {
        "tags": [
          "Hr"
        ],
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/api/hr/jobs": {
      "get": {
        "tags": [
          "Hr"
        ],
        "parameters": [
          {
            "name": "Page",
            "in": "query",
            "schema": {
              "type": "integer",
              "format": "int32"
            }
          },
          {
            "name": "PageSize",
            "in": "query",
            "schema": {
              "type": "integer",
              "format": "int32"
            }
          },
          {
            "name": "Department",
            "in": "query",
            "schema": {
              "type": "string"
            }
          },
          {
            "name": "ApprovalStatus",
            "in": "query",
            "schema": {
              "type": "string"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      },
      "post": {
        "tags": [
          "Hr"
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/CreateJobRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/CreateJobRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/CreateJobRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/api/hr/jobs/{jobId}": {
      "patch": {
        "tags": [
          "Hr"
        ],
        "parameters": [
          {
            "name": "jobId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          }
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/PatchJobRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/PatchJobRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/PatchJobRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      },
      "delete": {
        "tags": [
          "Hr"
        ],
        "parameters": [
          {
            "name": "jobId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/api/hr/candidates": {
      "get": {
        "tags": [
          "Hr"
        ],
        "parameters": [
          {
            "name": "page",
            "in": "query",
            "schema": {
              "type": "integer",
              "format": "int32",
              "default": 1
            }
          },
          {
            "name": "pageSize",
            "in": "query",
            "schema": {
              "type": "integer",
              "format": "int32",
              "default": 10
            }
          },
          {
            "name": "keyword",
            "in": "query",
            "schema": {
              "type": "string"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/api/hr/applications": {
      "get": {
        "tags": [
          "Hr"
        ],
        "parameters": [
          {
            "name": "page",
            "in": "query",
            "schema": {
              "type": "integer",
              "format": "int32",
              "default": 1
            }
          },
          {
            "name": "pageSize",
            "in": "query",
            "schema": {
              "type": "integer",
              "format": "int32",
              "default": 10
            }
          },
          {
            "name": "keyword",
            "in": "query",
            "schema": {
              "type": "string"
            }
          },
          {
            "name": "department",
            "in": "query",
            "schema": {
              "type": "string"
            }
          },
          {
            "name": "status",
            "in": "query",
            "schema": {
              "type": "string"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/api/hr/applications/{applicationId}/cv": {
      "get": {
        "tags": [
          "Hr"
        ],
        "parameters": [
          {
            "name": "applicationId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/api/hr/interviews": {
      "get": {
        "tags": [
          "Hr"
        ],
        "parameters": [
          {
            "name": "page",
            "in": "query",
            "schema": {
              "type": "integer",
              "format": "int32",
              "default": 1
            }
          },
          {
            "name": "pageSize",
            "in": "query",
            "schema": {
              "type": "integer",
              "format": "int32",
              "default": 10
            }
          },
          {
            "name": "keyword",
            "in": "query",
            "schema": {
              "type": "string"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      },
      "post": {
        "tags": [
          "Hr"
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/CreateInterviewRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/CreateInterviewRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/CreateInterviewRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/api/hr/interviews/{interviewId}/status": {
      "patch": {
        "tags": [
          "Hr"
        ],
        "parameters": [
          {
            "name": "interviewId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          }
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/UpdateInterviewStatusRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/UpdateInterviewStatusRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/UpdateInterviewStatusRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/api/hr/interviews/{interviewId}": {
      "delete": {
        "tags": [
          "Hr"
        ],
        "parameters": [
          {
            "name": "interviewId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/api/hr/interviews/schedule-data": {
      "get": {
        "tags": [
          "Hr"
        ],
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/api/jobs": {
      "get": {
        "tags": [
          "Job"
        ],
        "parameters": [
          {
            "name": "Page",
            "in": "query",
            "schema": {
              "type": "integer",
              "format": "int32"
            }
          },
          {
            "name": "PageSize",
            "in": "query",
            "schema": {
              "type": "integer",
              "format": "int32"
            }
          },
          {
            "name": "Keyword",
            "in": "query",
            "schema": {
              "type": "string"
            }
          },
          {
            "name": "EmploymentTypes",
            "in": "query",
            "schema": {
              "type": "array",
              "items": {
                "type": "string"
              }
            }
          },
          {
            "name": "Skills",
            "in": "query",
            "schema": {
              "type": "array",
              "items": {
                "type": "string"
              }
            }
          },
          {
            "name": "SortBy",
            "in": "query",
            "schema": {
              "type": "string"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/api/jobs/filters": {
      "get": {
        "tags": [
          "Job"
        ],
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/api/jobs/{jobId}": {
      "get": {
        "tags": [
          "Job"
        ],
        "parameters": [
          {
            "name": "jobId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/api/jobs/{jobId}/applications": {
      "get": {
        "tags": [
          "Job"
        ],
        "parameters": [
          {
            "name": "jobId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          },
          {
            "name": "page",
            "in": "query",
            "schema": {
              "type": "integer",
              "format": "int32",
              "default": 1
            }
          },
          {
            "name": "pageSize",
            "in": "query",
            "schema": {
              "type": "integer",
              "format": "int32",
              "default": 10
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/api/jobs/{jobId}/applications/recent": {
      "get": {
        "tags": [
          "Job"
        ],
        "parameters": [
          {
            "name": "jobId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/api/jobs/{jobId}/apply": {
      "post": {
        "tags": [
          "Job"
        ],
        "parameters": [
          {
            "name": "jobId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          }
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/ApplyJobRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/ApplyJobRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/ApplyJobRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/api/jobs/{jobId}/statistics": {
      "get": {
        "tags": [
          "Job"
        ],
        "parameters": [
          {
            "name": "jobId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/api/jobs/{jobId}/status": {
      "patch": {
        "tags": [
          "Job"
        ],
        "parameters": [
          {
            "name": "jobId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          }
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/UpdateJobStatusRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/UpdateJobStatusRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/UpdateJobStatusRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    }
  },
  "components": {
    "schemas": {
      "ApplyJobRequest": {
        "type": "object",
        "properties": {
          "coverLetter": {
            "type": "string",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "CandidateLoginRequest": {
        "type": "object",
        "properties": {
          "email": {
            "type": "string",
            "nullable": true
          },
          "password": {
            "type": "string",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "CreateInterviewRequest": {
        "type": "object",
        "properties": {
          "candidateId": {
            "type": "string",
            "nullable": true
          },
          "applicationId": {
            "type": "string",
            "nullable": true
          },
          "jobId": {
            "type": "string",
            "nullable": true
          },
          "date": {
            "type": "string",
            "format": "date"
          },
          "startMinutes": {
            "type": "integer",
            "format": "int32"
          },
          "durationMinutes": {
            "type": "integer",
            "format": "int32"
          },
          "mode": {
            "type": "string",
            "nullable": true
          },
          "locationOrLink": {
            "type": "string",
            "nullable": true
          },
          "interviewerId": {
            "type": "string",
            "nullable": true
          },
          "status": {
            "type": "string",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "CreateJobRequest": {
        "required": [
          "title"
        ],
        "type": "object",
        "properties": {
          "title": {
            "minLength": 1,
            "type": "string"
          },
          "department": {
            "type": "string",
            "nullable": true
          },
          "employmentType": {
            "type": "string",
            "nullable": true
          },
          "workMode": {
            "type": "string",
            "nullable": true
          },
          "location": {
            "type": "string",
            "nullable": true
          },
          "shortPitch": {
            "type": "string",
            "nullable": true
          },
          "description": {
            "type": "string",
            "nullable": true
          },
          "responsibilities": {
            "type": "array",
            "items": {
              "type": "string"
            },
            "nullable": true
          },
          "requirements": {
            "type": "array",
            "items": {
              "type": "string"
            },
            "nullable": true
          },
          "skills": {
            "type": "array",
            "items": {
              "type": "string"
            },
            "nullable": true
          },
          "salaryMin": {
            "type": "number",
            "format": "double",
            "nullable": true
          },
          "salaryMax": {
            "type": "number",
            "format": "double",
            "nullable": true
          },
          "currency": {
            "type": "string",
            "nullable": true
          },
          "vacancyCount": {
            "type": "integer",
            "format": "int32"
          }
        },
        "additionalProperties": false
      },
      "InternalLoginRequest": {
        "type": "object",
        "properties": {
          "employeeIdOrEmail": {
            "type": "string",
            "nullable": true
          },
          "password": {
            "type": "string",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "LoginRequest": {
        "type": "object",
        "properties": {
          "email": {
            "type": "string",
            "nullable": true
          },
          "password": {
            "type": "string",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "PatchJobRequest": {
        "type": "object",
        "properties": {
          "title": {
            "type": "string",
            "nullable": true
          },
          "department": {
            "type": "string",
            "nullable": true
          },
          "approvalStatus": {
            "type": "string",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "UpdateCandidateProfileRequest": {
        "type": "object",
        "properties": {
          "name": {
            "type": "string",
            "nullable": true
          },
          "headline": {
            "type": "string",
            "nullable": true
          },
          "email": {
            "type": "string",
            "nullable": true
          },
          "phone": {
            "type": "string",
            "nullable": true
          },
          "location": {
            "type": "string",
            "nullable": true
          },
          "bio": {
            "type": "string",
            "nullable": true
          },
          "github": {
            "type": "string",
            "nullable": true
          },
          "linkedin": {
            "type": "string",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "UpdateCandidateSkillsRequest": {
        "type": "object",
        "properties": {
          "skillIds": {
            "type": "array",
            "items": {
              "type": "string"
            },
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "UpdateInterviewStatusRequest": {
        "type": "object",
        "properties": {
          "status": {
            "type": "string",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "UpdateJobStatusRequest": {
        "type": "object",
        "properties": {
          "status": {
            "type": "string",
            "nullable": true
          }
        },
        "additionalProperties": false
      }
    }
  }
}